using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Realizes every e-mail the send-email queue carries, off the request path. The queue holds seven
/// payload shapes told apart by their <c>messageType</c> discriminator: the bare
/// <see cref="SendEmailMessage"/> (no discriminator; confirmation, reset, promo and the two wind-down
/// notices, resolved by <see cref="EmailType"/>), the guest order cancellation, the booking
/// confirmation, the admin notification, the receivable pay link, the lockout cancellation and the
/// cleaner's copy of a contract for work. Each is sent via the existing
/// <see cref="IEmailService"/> in the language the producer chose.
///
/// Idempotent via <see cref="IIdempotencyGuard"/> in ACT-THEN-CLAIM mode (at-least-once): non-claiming
/// check on the deterministic key → send → claim. A FAILED send leaves the key unclaimed so the queue
/// retry genuinely retries — the previous claim-then-act permanently lost any email whose send failed
/// after the claim. The accepted residual is a rare duplicate email, never a lost one; claim-then-act
/// stays mandatory for consumers whose effect is not safely repeatable (anything money-shaped).
/// Dual-reads the bare in-flight payload at the deploy boundary, synthesizing the same key from the
/// payload. Classifies failures: a malformed / business-rejected body acks (no throw); an
/// infra/transport fault throws so the runtime retries to maxDequeueCount and then dead-letters.
/// </summary>
public class SendEmailHandler(
    IEmailService emailService,
    IIdempotencyGuard idempotencyGuard,
    ITenantProvider tenantProvider,
    IPromoCodeRepository promoCodeRepository,
    ITenantRepository tenantRepository,
    ICompanyInfoRepository companyInfoRepository,
    ILogger<SendEmailHandler> logger,
    IOrderRepository orderRepository,
    GuestOrderAccessTokenIssuer guestAccessTokenIssuer,
    IUnitOfWork unitOfWork,
    ICancellationPolicyResolver cancellationPolicyResolver,
    IReceivableRepository receivableRepository,
    IContractConfirmationService contractConfirmations,
    IWorkContractAcceptanceRepository workContractAcceptanceRepository,
    IEmployeeRepository employeeRepository)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task HandleAsync(string messageText, CancellationToken ct)
    {
        SendGuestOrderCancellationEmailMessage? guestMessage;
        SendOrderBookedEmailMessage? bookedMessage;
        SendAdminNotificationEmailMessage? adminMessage;
        SendReceivablePayLinkEmailMessage? payLinkMessage;
        SendOrderLockoutEmailMessage? lockoutMessage;
        SendWorkContractEmailMessage? workContractMessage;
        string? discriminatedTenantId;
        try
        {
            using var document = JsonDocument.Parse(messageText);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            var payload = root.TryGetProperty("payload", out var nested) ? nested : root;
            var messageType = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("messageType", out var kind)
                && kind.ValueKind == JsonValueKind.String ? kind.GetString() : null;
            guestMessage = messageType == "guest-order-cancelled"
                ? payload.Deserialize<SendGuestOrderCancellationEmailMessage>(JsonOptions) : null;
            bookedMessage = messageType == SendOrderBookedEmailMessage.Discriminator
                ? payload.Deserialize<SendOrderBookedEmailMessage>(JsonOptions) : null;
            adminMessage = messageType == SendAdminNotificationEmailMessage.Discriminator
                ? payload.Deserialize<SendAdminNotificationEmailMessage>(JsonOptions) : null;
            payLinkMessage = messageType == SendReceivablePayLinkEmailMessage.Discriminator
                ? payload.Deserialize<SendReceivablePayLinkEmailMessage>(JsonOptions) : null;
            lockoutMessage = messageType == SendOrderLockoutEmailMessage.Discriminator
                ? payload.Deserialize<SendOrderLockoutEmailMessage>(JsonOptions) : null;
            workContractMessage = messageType == SendWorkContractEmailMessage.Discriminator
                ? payload.Deserialize<SendWorkContractEmailMessage>(JsonOptions) : null;
            discriminatedTenantId = root.TryGetProperty("tenantId", out var tenant) && tenant.ValueKind == JsonValueKind.String ? tenant.GetString() : null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Discarding email message: malformed body (permanent)");
            return;
        }
        if (guestMessage is not null)
        {
            await SendGuestCancellationAsync(guestMessage, discriminatedTenantId, ct);
            return;
        }
        if (bookedMessage is not null)
        {
            await SendOrderBookedAsync(bookedMessage, discriminatedTenantId, ct);
            return;
        }
        if (adminMessage is not null)
        {
            await SendAdminNotificationAsync(adminMessage, discriminatedTenantId, ct);
            return;
        }
        if (payLinkMessage is not null)
        {
            await SendReceivablePayLinkAsync(payLinkMessage, discriminatedTenantId, ct);
            return;
        }
        if (lockoutMessage is not null)
        {
            await SendOrderLockoutAsync(lockoutMessage, discriminatedTenantId, ct);
            return;
        }
        if (workContractMessage is not null)
        {
            await SendWorkContractAsync(workContractMessage, discriminatedTenantId, ct);
            return;
        }

        SendEmailMessage? message;
        string? envelopeTenantId;
        try
        {
            (message, envelopeTenantId) = ReadPayload(messageText);
        }
        catch (JsonException ex)
        {
            // Never log the payload — it carries the recipient email and a live confirmation/reset code.
            logger.LogWarning(ex, "Discarding email message: malformed body (permanent)");
            return;
        }

        if (message is null)
        {
            logger.LogWarning("Discarding email message: empty payload (permanent)");
            return;
        }

        var missingFields = new[]
            {
                (Name: nameof(message.UserId), Value: message.UserId),
                (Name: nameof(message.Email), Value: message.Email),
                (Name: nameof(message.Code), Value: message.Code),
            }
            .Where(f => string.IsNullOrEmpty(f.Value))
            .Select(f => f.Name)
            .ToList();

        if (missingFields.Count > 0)
        {
            logger.LogWarning(
                "Discarding {EmailType} email message with missing required fields (permanent): {MissingFields}",
                message.EmailType, string.Join(", ", missingFields));
            return;
        }

        var messageKey = MessageKeys.Email(message.EmailType, message.UserId, MessageKeys.HashCode(message.Code));

        if (await idempotencyGuard.HasProcessedAsync(messageKey, ct))
        {
            logger.LogInformation("Email {MessageKey} already sent, skipping (idempotent)", messageKey);
            return;
        }

        try
        {
            var tenantId = !string.IsNullOrEmpty(envelopeTenantId) ? envelopeTenantId : message.TenantId;
            if (!string.IsNullOrEmpty(tenantId))
            {
                tenantProvider.SetTenantOverride(tenantId);
            }

            await SendAsync(message, ct);

            logger.LogInformation("Sent {EmailType} email to user {UserId} (key {MessageKey})",
                message.EmailType, message.UserId, messageKey);
        }
        catch (Exception ex)
        {
            // The key is still unclaimed, so this redelivery genuinely re-attempts the send.
            logger.LogError(ex,
                "Transient/infra failure sending {EmailType} email for user {UserId} — will retry via queue",
                message.EmailType, message.UserId);
            throw;
        }

        try
        {
            await idempotencyGuard.MarkProcessedAsync(messageKey, ct);
        }
        catch (Exception ex)
        {
            // The email IS sent; throwing here would force an immediate redelivery — a guaranteed
            // duplicate. Log and ack: the residual is a rare duplicate IF this message is redelivered
            // while its key remains unclaimed, which is the accepted worst case for a notification email.
            logger.LogWarning(ex,
                "Sent {EmailType} email to user {UserId} but failed to record the idempotency claim (key {MessageKey}) — acking; a redelivery may duplicate this email",
                message.EmailType, message.UserId, messageKey);
        }
    }

    private async Task SendGuestCancellationAsync(
        SendGuestOrderCancellationEmailMessage message, string? envelopeTenantId, CancellationToken ct)
    {
        var tenantId = envelopeTenantId ?? message.TenantId;
        if (string.IsNullOrWhiteSpace(message.OrderId) || string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Discarding guest cancellation email with no order or operator");
            return;
        }
        var key = MessageKeys.GuestOrderCancelledEmail(message.OrderId);
        if (await idempotencyGuard.HasProcessedAsync(key, ct)) return;

        tenantProvider.SetTenantOverride(tenantId);
        var order = await orderRepository.GetQueryable()
            .Include(o => o.Currency).Include(o => o.CustomerAddress)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == message.OrderId && o.UserId == null
                && o.CurrentStatus == OrderStatus.Cancelled
                && (o.CancelledBy == CancelledBy.Customer || o.CancelledBy == CancelledBy.Admin
                    || o.CancelledBy == CancelledBy.System), ct);
        if (order is null || string.IsNullOrWhiteSpace(order.CustomerEmail)
            || order.CustomerEmail == AnonymizationMarker.Value)
        {
            logger.LogWarning("Discarding guest cancellation email: order {OrderId} has no eligible destination", message.OrderId);
            return;
        }

        // A fresh credential for a booking whose keys were all just retired, and the two are the same
        // decision rather than opposite ones: cancelling withdraws every key that was outstanding, and
        // this — the last message the booking will ever send — carries the only one that still opens
        // it, so the customer can read what they were refunded. Committed before the send, so a crash
        // after it cannot leave an e-mailed token with no row behind it.
        var guestAccessToken = guestAccessTokenIssuer.IssueForGuest(order);
        if (guestAccessToken is not null)
        {
            await unitOfWork.CommitAsync(ct);
        }

        await emailService.SendOrderStatusUpdateEmailAsync(order.CustomerEmail, order, "Cancelled",
            message.LanguageCode, ct, message.SuccessfulRefundAmount, guestAccessToken);
        try
        {
            await idempotencyGuard.MarkProcessedAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Guest cancellation email sent for order {OrderId}, but its delivery claim failed", order.Id);
        }
    }

    // Act-then-claim like the guest cancellation beside it. A booking cancelled before the message was
    // read is not confirmed to anyone.
    private async Task SendOrderBookedAsync(
        SendOrderBookedEmailMessage message, string? envelopeTenantId, CancellationToken ct)
    {
        var tenantId = envelopeTenantId ?? message.TenantId;
        if (string.IsNullOrWhiteSpace(message.OrderId) || string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Discarding booking e-mail with no order or operator");
            return;
        }
        var key = MessageKeys.OrderBookedEmail(message.OrderId);
        if (await idempotencyGuard.HasProcessedAsync(key, ct)) return;

        tenantProvider.SetTenantOverride(tenantId);
        var order = await orderRepository.GetQueryable()
            .Include(o => o.Currency).Include(o => o.CustomerAddress).Include(o => o.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == message.OrderId
                && o.CurrentStatus != OrderStatus.Cancelled, ct);
        if (order is null || string.IsNullOrWhiteSpace(order.CustomerEmail)
            || order.CustomerEmail == AnonymizationMarker.Value)
        {
            logger.LogWarning("Discarding booking e-mail: order {OrderId} has no eligible destination", message.OrderId);
            return;
        }

        var policy = await cancellationPolicyResolver.ResolveForOrderAsync(order, ct);
        var languageCode = EmailLocale.Resolve(
            order.LanguageCode ?? order.User?.PreferredLanguageCode ?? message.LanguageCode);
        var confirmation = await contractConfirmations.ForBookingAsync(
            order, message.ContractConcludedOn ?? order.CreatedOn, languageCode, ct);

        // The guest's link to the booking, committed before the send so an e-mailed token always has
        // its row — the receipt that used to carry it now comes after the clean.
        var guestAccessToken = guestAccessTokenIssuer.IssueForGuest(order);
        if (guestAccessToken is not null)
        {
            await unitOfWork.CommitAsync(ct);
        }

        await emailService.SendOrderBookedEmailAsync(order.CustomerEmail, order, policy.FreeCancellationHours,
            languageCode, ct, guestAccessToken, confirmation.Bytes, confirmation.FileName);
        try
        {
            await idempotencyGuard.MarkProcessedAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Booking e-mail sent for order {OrderId}, but its delivery claim failed", order.Id);
        }
    }

    // Act-then-claim like the shapes beside it.
    private async Task SendWorkContractAsync(
        SendWorkContractEmailMessage message, string? envelopeTenantId, CancellationToken ct)
    {
        var tenantId = envelopeTenantId ?? message.TenantId;
        if (string.IsNullOrWhiteSpace(message.AcceptanceId) || string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Discarding work-contract e-mail with no acceptance or operator");
            return;
        }
        var key = MessageKeys.WorkContractEmail(message.AcceptanceId);
        if (await idempotencyGuard.HasProcessedAsync(key, ct)) return;

        tenantProvider.SetTenantOverride(tenantId);
        var acceptance = await workContractAcceptanceRepository.GetByIdAsync(message.AcceptanceId, ct);
        var cleaner = acceptance is null ? null : await employeeRepository.GetByIdAsync(acceptance.EmployeeId, ct);
        if (acceptance is null || cleaner?.User is not { Email.Length: > 0 } user)
        {
            logger.LogWarning("Discarding work-contract e-mail: acceptance {AcceptanceId} has no eligible destination", message.AcceptanceId);
            return;
        }

        var languageCode = EmailLocale.Resolve(user.PreferredLanguageCode);
        var contract = await contractConfirmations.ForWorkContractAsync(acceptance, cleaner, languageCode, ct);
        await emailService.SendWorkContractEmailAsync(
            user.Email, $"{user.FirstName} {user.LastName}".Trim(), WorkContractFacts.FromJson(acceptance.FactsJson).OrderNumber,
            contract.Bytes, contract.FileName, languageCode, ct);
        try
        {
            await idempotencyGuard.MarkProcessedAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Work-contract e-mail sent for acceptance {AcceptanceId}, but its delivery claim failed", acceptance.Id);
        }
    }

    // Act-then-claim like the shapes beside it.
    private async Task SendOrderLockoutAsync(
        SendOrderLockoutEmailMessage message, string? envelopeTenantId, CancellationToken ct)
    {
        var tenantId = envelopeTenantId ?? message.TenantId;
        if (string.IsNullOrWhiteSpace(message.OrderId) || string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Discarding lockout e-mail with no order or operator");
            return;
        }
        var key = MessageKeys.OrderLockoutEmail(message.OrderId);
        if (await idempotencyGuard.HasProcessedAsync(key, ct)) return;

        tenantProvider.SetTenantOverride(tenantId);
        var order = await orderRepository.GetQueryable()
            .Include(o => o.Currency).Include(o => o.CustomerAddress).Include(o => o.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == message.OrderId
                && o.CurrentStatus == OrderStatus.Cancelled
                && o.CancellationReason == OrderCancellationReasons.CustomerLockout, ct);
        if (order is null || string.IsNullOrWhiteSpace(order.CustomerEmail)
            || order.CustomerEmail == AnonymizationMarker.Value)
        {
            logger.LogWarning("Discarding lockout e-mail: order {OrderId} has no eligible destination", message.OrderId);
            return;
        }

        var languageCode = EmailLocale.Resolve(
            order.LanguageCode ?? order.User?.PreferredLanguageCode ?? message.LanguageCode);

        // Null for the account booking this message is sent for; a guest's link is committed before the send
        // so an e-mailed token always has its row.
        var guestAccessToken = guestAccessTokenIssuer.IssueForGuest(order);
        if (guestAccessToken is not null)
        {
            await unitOfWork.CommitAsync(ct);
        }

        await emailService.SendOrderStatusUpdateEmailAsync(order.CustomerEmail, order, "Cancelled", languageCode, ct,
            guestAccessToken: guestAccessToken);
        try
        {
            await idempotencyGuard.MarkProcessedAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lockout e-mail sent for order {OrderId}, but its delivery claim failed", order.Id);
        }
    }

    // Act-then-claim like the shapes beside it. A receivable settled or written off before the send is not
    // chased: the link would take money no longer owed.
    private async Task SendReceivablePayLinkAsync(
        SendReceivablePayLinkEmailMessage message, string? envelopeTenantId, CancellationToken ct)
    {
        var tenantId = envelopeTenantId ?? message.TenantId;
        if (string.IsNullOrWhiteSpace(message.ReceivableId) || string.IsNullOrWhiteSpace(message.PayUrl)
            || string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Discarding pay-link e-mail with no receivable, link or operator");
            return;
        }
        var key = MessageKeys.ReceivablePayLinkEmail(message.ReceivableId, message.Attempt);
        if (await idempotencyGuard.HasProcessedAsync(key, ct)) return;

        tenantProvider.SetTenantOverride(tenantId);
        var receivable = await receivableRepository.GetByIdAsync(message.ReceivableId, ct);
        if (receivable is not { IsOpen: true })
        {
            logger.LogInformation("Pay-link e-mail for receivable {ReceivableId} not sent: it is no longer open", message.ReceivableId);
            return;
        }

        var order = await orderRepository.GetQueryable()
            .Include(o => o.Currency).Include(o => o.CustomerAddress).Include(o => o.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == receivable.OrderId, ct);
        if (order is null || string.IsNullOrWhiteSpace(order.CustomerEmail)
            || order.CustomerEmail == AnonymizationMarker.Value)
        {
            logger.LogWarning("Discarding pay-link e-mail: order {OrderId} has no eligible destination", receivable.OrderId);
            return;
        }

        var languageCode = EmailLocale.Resolve(order.LanguageCode ?? order.User?.PreferredLanguageCode ?? Constants.Language.English);
        await emailService.SendReceivablePayLinkEmailAsync(order.CustomerEmail, order, receivable, message.PayUrl, languageCode, ct);
        try
        {
            await idempotencyGuard.MarkProcessedAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pay-link e-mail sent for receivable {ReceivableId}, but its delivery claim failed", receivable.Id);
        }
    }

    // Act-then-claim like the shapes beside it: the send is the only thing that may throw, and it
    // throws so the runtime retries and dead-letters; a body the producer could never have written acks.
    private async Task SendAdminNotificationAsync(
        SendAdminNotificationEmailMessage message, string? envelopeTenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.EventKey) || string.IsNullOrWhiteSpace(message.Subject)
            || string.IsNullOrWhiteSpace(message.Email) || message.Args is null)
        {
            logger.LogWarning("Discarding admin notification email with no event, subject, address or args (permanent)");
            return;
        }

        var key = MessageKeys.AdminNotificationEmail(message.EventKey, message.Subject, message.Email);
        if (await idempotencyGuard.HasProcessedAsync(key, ct))
        {
            logger.LogInformation("Admin notification email {MessageKey} already sent, skipping (idempotent)", key);
            return;
        }

        var tenantId = envelopeTenantId ?? message.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            tenantProvider.SetTenantOverride(tenantId);
        }

        await emailService.SendAdminNotificationEmailAsync(message.Email, message.EventKey, message.Args, message.LanguageCode, ct);

        try
        {
            await idempotencyGuard.MarkProcessedAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Admin notification email {MessageKey} sent, but its delivery claim failed — acking; a redelivery may duplicate it", key);
        }
    }

    private Task SendAsync(SendEmailMessage message, CancellationToken ct) => message.EmailType switch
    {
        EmailType.ConfirmationEmail =>
            emailService.SendEmailConfirmationAsync(message.Email, message.UserName, message.Code, message.LanguageCode, ct),
        EmailType.ResetPassword =>
            emailService.SendResetPasswordEmailAsync(message.Email, message.UserName, message.Code, message.LanguageCode, ct),
        // The promo body needs the discount and the expiry, which the message does
        // not carry — they live on the row the command just wrote, keyed by the
        // same code. Reading them here keeps the message shape unchanged.
        EmailType.PromoCode => SendPromoAsync(message, ct),
        // The wind-down notice names the company as its receipts do and the last day of service —
        // both on the company's own rows, read under the override the envelope set, so the message
        // shape stays the frozen one.
        EmailType.CompanyWindDownCustomer => SendCompanyWindDownAsync(message, ct),
        EmailType.CompanyWindDownCleaner => SendCompanyWindDownAsync(message, ct),
        _ => throw new InvalidOperationException($"Unsupported email type for the send-email queue: {message.EmailType}"),
    };

    private async Task SendCompanyWindDownAsync(SendEmailMessage message, CancellationToken ct)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var company = string.IsNullOrEmpty(tenantId) ? null : await tenantRepository.GetByIdAsync(tenantId, ct);
        if (company?.WindDownFrom is not { } windDownFrom)
        {
            // The date was cleared by a reactivation after the notice was enqueued. Like a promo code
            // deleted after its e-mail was queued, this throws rather than acks: the poison row is how
            // a person learns a notice was asked for and not sent.
            throw new InvalidOperationException(
                $"Company {tenantId} has no wind-down date; refusing to send a wind-down notice for it.");
        }

        var companyNames = await companyInfoRepository.GetActiveLegalNamesAsync(ct);

        if (message.EmailType == EmailType.CompanyWindDownCleaner)
        {
            await emailService.SendCompanyWindDownCleanerNoticeAsync(
                message.Email, message.UserName, companyNames, windDownFrom, message.LanguageCode, ct);
            return;
        }

        await emailService.SendCompanyWindDownCustomerNoticeAsync(
            message.Email, message.UserName, companyNames, windDownFrom, message.LanguageCode, ct);
    }

    private async Task SendPromoAsync(SendEmailMessage message, CancellationToken ct)
    {
        var promo = await promoCodeRepository.GetByCodeAsync(message.Code, ct);

        if (promo is null)
        {
            // The row is written in the same transaction that queued this message,
            // so its absence means it was deleted after the fact. Nothing to send.
            throw new InvalidOperationException(
                $"Promo code {message.Code} no longer exists; refusing to send an e-mail advertising it.");
        }

        var discountLabel = promo.DiscountPercent is { } percent
            ? $"-{percent * 100m:0.#} %"
            : $"-{promo.DiscountAmount:0.##}";

        await emailService.SendPromoCodeEmailAsync(
            message.Email,
            promo.Code,
            discountLabel,
            promo.ValidUntil?.UtcDateTime,
            message.LanguageCode,
            ct);
    }

    private static (SendEmailMessage? Message, string? EnvelopeTenantId) ReadPayload(string messageText)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<QueueEnvelope<SendEmailMessage>>(messageText, JsonOptions);
            if (envelope?.Payload is { UserId.Length: > 0 } payload)
            {
                return (payload, envelope.TenantId);
            }
        }
        catch (JsonException)
        {
            // Fall through to the bare-payload read below.
        }

        return (JsonSerializer.Deserialize<SendEmailMessage>(messageText, JsonOptions), null);
    }
}
