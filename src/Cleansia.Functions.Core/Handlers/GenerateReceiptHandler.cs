using System.Text.Json;
using System.Text.RegularExpressions;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Fiscal.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

public class GenerateReceiptHandler(
    IOrderRepository orderRepository,
    IReceiptService receiptService,
    IEmailService emailService,
    ICountryConfigurationRepository countryConfigurationRepository,
    IUnitOfWork unitOfWork,
    ITenantProvider tenantProvider,
    ILogger<GenerateReceiptHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        GenerateReceiptMessage? message = null;

        try
        {
            // ADR-0002 D2.1a — DUAL-READ at the deploy boundary. The producer now wraps the payload in
            // a QueueEnvelope<T> ({"messageKey","tenantId","payload":{...}}), but bare messages may be
            // in-flight when this consumer deploys. Try the envelope first; on a missing/empty payload
            // fall back to the bare message and SYNTHESIZE the deterministic key from the payload
            // (MessageKeys.Receipt) so in-flight bare messages are processed, not poisoned.
            message = ReadPayload(messageText);
            if (message is null)
            {
                throw new InvalidOperationException($"Failed to deserialize GenerateReceiptMessage: {messageText}");
            }

            // The idempotency key is deterministic from the order id (envelope or synthesized) — used
            // for log correlation; the load-bearing dedup is the receipt-creation guard below (the
            // committed receipt row IS the claim, written before the email — claim-first, D2.2).
            var messageKey = MessageKeys.Receipt(message.OrderId ?? string.Empty);

            if (string.IsNullOrEmpty(message.OrderId) || !UlidPattern.IsMatch(message.OrderId))
            {
                logger.LogWarning("Discarding receipt message with invalid OrderId format: {OrderId}", message.OrderId);
                return;
            }

            logger.LogInformation("Processing receipt generation for order {OrderId} (key {MessageKey})",
                message.OrderId, messageKey);

            // Queue trigger — no JWT context. Look up cross-tenant by trusted
            // OrderId, then set tenant override so OrderReceipt and other
            // child writes inherit the right TenantId.
            var order = await orderRepository.GetByIdIgnoringTenantAsync(message.OrderId, ct);
            if (order is null)
            {
                // Fiscal-queue carve-out (ADR-0002 D3.3): "target not found" stays TRANSIENT /
                // bounded-retry — it must NOT be reclassified to a permanent ack. Throwing lets the
                // queue retry (and lets the CH-1 reconciliation re-enqueue race brief read-replica lag).
                logger.LogWarning("Order {OrderId} not found, will retry via queue visibility timeout", message.OrderId);
                throw new InvalidOperationException($"Order {message.OrderId} not found");
            }

            if (!string.IsNullOrEmpty(order.TenantId))
            {
                tenantProvider.SetTenantOverride(order.TenantId);
            }

            if (order.PaymentType != PaymentType.Cash && order.PaymentStatus != PaymentStatus.Paid)
            {
                logger.LogWarning("Discarding receipt message for order {OrderId}: not eligible (PaymentType={Type}, PaymentStatus={Status})",
                    message.OrderId, order.PaymentType, order.PaymentStatus);
                return;
            }

            // ADR-0002 D2.2 / ADR-0004 D-F4.1 — the idempotency guard (retained). The committed receipt
            // row is the durable CLAIM: once it exists, a redelivery (or a duplicate enqueue from the
            // Stripe-retry hazard) is recognized as already-done and short-circuits BEFORE re-burning a
            // fiscal sequence, re-registering with the authority, or re-sending the terminal email.
            if (order.Receipt is not null)
            {
                logger.LogInformation("Receipt already exists for order {OrderId}, skipping (idempotent: {MessageKey})",
                    message.OrderId, messageKey);
                return;
            }

            // ── ADR-0004 D-F4.1 phase 1 — RESERVE + COMMIT THE CLAIM (before the irreversible effect) ──
            // Allocate the sequence + stage the receipt row (born retry-eligible for any fiscal mode !=
            // None), then COMMIT it NOW — before the authority register and before the PDF. This is the
            // ordering inversion that makes the irreversible external effect at-most-once: after this
            // commit every redelivery short-circuits on `order.Receipt is not null` above, so the
            // sequence is never re-burned and the authority is never re-registered. A crash BEFORE this
            // commit leaves no row and no external effect, so re-running phase 1 is safe.
            var receipt = await receiptService.ReserveReceiptAsync(order, message.LanguageCode, ct);

            try
            {
                await unitOfWork.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // ADR-0004 D-F4.1(b) — DB backstop. Two concurrent first-deliveries can both pass the
                // guard above and both attempt this claim commit. The existing unique index makes the
                // loser throw PG 23505 — on EITHER IX_OrderReceipts_OrderId OR (because the allocator is
                // still COUNT(*)+1 until FISCAL-SEQ/T-0220) IX_OrderReceipts_ReceiptNumber. Treat that as
                // ALREADY-CLAIMED and collapse to an ACK: the winner owns the single row, the single
                // register, and the single email; the loser must NOT throw (no poison loop). Genuine
                // infra faults are NOT unique-violations, so they still bubble out of the outer catch.
                logger.LogInformation(ex,
                    "Concurrent receipt claim collapsed for order {OrderId} (unique-violation 23505) — already claimed, ack",
                    message.OrderId);
                return;
            }

            // ── ADR-0004 D-F4.1 phase 2 — REALIZE the external effects for the now-durable claim ──
            // Register with the fiscal authority (stamp on success / mark failed on failure) and
            // generate + upload the PDF. A redelivery during/after this step is already deduped by the
            // committed claim above, so the authority is never registered twice for this OrderId.
            await receiptService.RealizeFiscalAndPdfAsync(order, receipt, message.LanguageCode, ct);

            // Persist the fiscal stamp (FiscalCode / failure markers) written during realize. The dedup
            // is already secured by the claim commit; this second commit records the fiscal result.
            await unitOfWork.CommitAsync(ct);

            // ── ADR-0004 D-F4.1 phase 3 / D2.2 — TERMINAL EMAIL (blocking-mode hold preserved) ──
            // BlockingOnline countries (DE TSE, AT RKSV, ES VeriFactu) legally require the fiscal
            // signature on the receipt before it can be delivered. If the register did not yield a
            // FiscalCode, HOLD the email — the claim is already committed (so a redelivery is deduped)
            // and the born-retry-eligible row will be completed by the retry job, which releases the
            // held email once the authority signs.
            var enforcementMode = await ResolveEnforcementModeAsync(order, ct);
            var isBlockingMode = enforcementMode is FiscalEnforcementMode.BlockingOnline
                or FiscalEnforcementMode.BlockingWithOfflineCache;

            if (isBlockingMode && receipt.FiscalCode == null)
            {
                logger.LogWarning(
                    "Holding receipt email for order {OrderId} — country requires fiscal signature and retry is pending",
                    message.OrderId);
                return;
            }

            var pdfBytes = await receiptService.DownloadReceiptPdfAsync(receipt, ct);
            logger.LogInformation("Receipt PDF downloaded ({Size} bytes), sending email...", pdfBytes.Length);

            var emailMessageId = await emailService.SendOrderReceiptEmailAsync(
                order.CustomerEmail, order, pdfBytes, receipt.FileName, message.LanguageCode, ct);

            // Best-effort metadata stamp. The dedup is already secured by the claim commit above; this
            // commit only records the provider message id for observability and never re-opens the
            // re-send window (a failure here still leaves the receipt committed → still deduped). The
            // accepted Wave-0 residual is the rare lost email on a crash between the claim and the send
            // (recoverable by reconciliation), NOT a double-send.
            receipt.MarkEmailSent(emailMessageId);
            await unitOfWork.CommitAsync(ct);

            logger.LogInformation("Receipt generated and email sent for order {OrderId}", message.OrderId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate receipt for order {OrderId}. Message: {Message}",
                message?.OrderId ?? "unknown", messageText);
            throw; // Re-throw so Azure Functions retries via queue (D3.3: target-not-found stays transient)
        }
    }

    /// <summary>
    /// ADR-0004 D-F4.1(b) — true when the <see cref="DbUpdateException"/> was caused by a Postgres
    /// unique-constraint violation (SQLSTATE 23505): the existing unique index rejecting a concurrent
    /// loser's claim insert, on EITHER <c>IX_OrderReceipts_OrderId</c> OR
    /// <c>IX_OrderReceipts_ReceiptNumber</c>. Either is "already-claimed" → ack, not poison. Detected
    /// provider-agnostically by duck-typing the inner exception's public <c>SqlState</c> property (this
    /// library carries no hard Npgsql reference), walking the whole inner chain because EF may wrap the
    /// provider exception more than one level deep. Mirrors
    /// <c>CreateMembershipSubscription.Handler.IsUniqueViolation</c> (T-0111) / LoyaltyService (T-0112)
    /// / StripeSubscriptionWebhookHandler (T-0114).
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        const string UniqueViolation = "23505";
        for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            var sqlState = inner.GetType()
                .GetProperty("SqlState")?
                .GetValue(inner) as string;
            if (sqlState == UniqueViolation)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ADR-0002 D2.1a dual-read. Returns the <see cref="GenerateReceiptMessage"/> payload from either
    /// the new <see cref="QueueEnvelope{T}"/> wire shape or the bare (pre-envelope) message; returns
    /// <c>null</c> when neither shape yields a usable payload.
    /// </summary>
    private static GenerateReceiptMessage? ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<GenerateReceiptMessage>>(messageText, JsonOptions);
            if (envelope?.Payload is { OrderId: not null } payload)
            {
                return payload;
            }
        }
        catch (JsonException)
        {
            // Fall through to the bare-payload read below.
        }

        try
        {
            return JsonSerializer.Deserialize<GenerateReceiptMessage>(messageText, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly Regex UlidPattern = new("^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.Compiled);

    private async Task<FiscalEnforcementMode> ResolveEnforcementModeAsync(
        Cleansia.Core.Domain.Orders.Order order,
        CancellationToken cancellationToken)
    {
        var countryId = order.CustomerAddress?.CountryId;
        if (countryId == null)
        {
            return FiscalEnforcementMode.None;
        }

        var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
        return countryConfig?.FiscalEnforcementMode ?? FiscalEnforcementMode.None;
    }
}
