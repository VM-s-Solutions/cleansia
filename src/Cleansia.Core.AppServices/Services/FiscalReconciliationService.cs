using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// ADR-0002 D3.4 + ADR-0004 C-B — the DISPATCH-layer reconciliation sweep.
/// Wave-0 dispatch is at-most-once: a crash between the commit and the in-memory drain loses
/// the send, leaving NO message → no <c>-poison</c> → no alert (the F3 poison floor only catches
/// enqueued-and-failed-5x). For the two fiscal queues that silent loss is a lost legal/financial
/// artifact, so this sweep finds committed-but-unrealized fiscal work and RE-ENQUEUES it through the
/// SAME idempotent path — harmlessly deduped downstream by the deterministic <see cref="MessageKeys"/>
/// + the consumer's target-state guard.
///
/// <para>This is a Bucket-B system-context loop (per item, no per-request pipeline to gate), so it
/// calls <see cref="IQueueClient"/> DIRECTLY under the documented Bucket-B carve-out (ADR-0002 D5
/// Bucket B, reviewer check #1 whitelist) — NOT the request-scoped <c>IPendingDispatch</c>. Each
/// message is wrapped in the SAME <see cref="QueueEnvelope{T}"/> + frozen <see cref="MessageKeys"/>
/// so the re-enqueue dedups downstream.</para>
///
/// <para>Mirrors the batch + tenant-override pattern of <c>FiscalRetryService</c> but is DISTINCT from
/// it: that is the registration-retry layer (re-register an already-claimed receipt); this is the
/// dispatch layer (re-enqueue the missing message). They are not merged.</para>
/// </summary>
public sealed class FiscalReconciliationService(
    IOrderRepository orderRepository,
    IPayPeriodRepository payPeriodRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    IQueueClient queueClient,
    ITenantProvider tenantProvider,
    IFiscalReconciliationConfig config,
    ILogger<FiscalReconciliationService> logger)
    : IFiscalReconciliationService
{
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        // The cutoff is a tunable, not a business value (ADR-0002 D3.4): items committed WITHIN the
        // window (their normal post-commit dispatch may still be on the wire) are NOT swept.
        var cutoff = DateTime.UtcNow.AddMinutes(-config.ThresholdMinutes);
        var batchSize = config.BatchSize;

        var reEnqueued = 0;
        reEnqueued += await ReconcileReceiptsAsync(cutoff, batchSize, cancellationToken);
        reEnqueued += await ReconcileInvoicesAsync(cutoff, batchSize, cancellationToken);
        return reEnqueued;
    }

    private async Task<int> ReconcileReceiptsAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var candidates = await orderRepository.GetReceiptReconciliationCandidatesAsync(cutoff, batchSize, cancellationToken);
        if (candidates.Count == 0)
        {
            return 0;
        }

        // memoize the tiny, near-static country→enforcement-mode config for the duration
        // of THIS sweep so we don't issue one GetByCountryIdAsync round-trip per candidate (up to
        // batchSize uncached reads, 288x/day). One read per distinct country, reused across the batch.
        var enforcementModeByCountry = new Dictionary<string, FiscalEnforcementMode>(StringComparer.Ordinal);

        var reEnqueued = 0;
        foreach (var order in candidates)
        {
            // System job — no JWT. Reset before each item so a previous multi-tenant item's override
            // doesn't leak into a single-tenant one (FiscalRetryService.cs:42-48 pattern).
            tenantProvider.ClearTenantOverride();
            if (!string.IsNullOrEmpty(order.TenantId))
            {
                tenantProvider.SetTenantOverride(order.TenantId);
            }

            // ADR-0004 C-B exact predicate: Receipt is null is ALWAYS swept; the
            // claimed-but-unregistered case (Receipt present, FiscalCode == null) is swept ONLY when the
            // country enforces fiscal registration (enforcementMode != None). For a None-mode country a
            // null FiscalCode is the steady state, not a missing artifact.
            if (order.Receipt is not null)
            {
                var enforcementMode = await ResolveEnforcementModeAsync(order, enforcementModeByCountry, cancellationToken);
                if (enforcementMode == FiscalEnforcementMode.None)
                {
                    continue;
                }
            }

            var languageCode = order.Receipt?.Language?.Code ?? Constants.Language.English;
            var key = MessageKeys.Receipt(order.Id);

            try
            {
                // Bucket-B carve-out: re-enqueue directly via IQueueClient, wrapped in the SAME
                // QueueEnvelope<T> + frozen key the consumer dual-reads (serialized camelCase by the
                // client, identical to InMemoryPendingDispatch).
                await queueClient.SendAsync(
                    QueueNames.GenerateReceipt,
                    new QueueEnvelope<GenerateReceiptMessage>(
                        key, order.TenantId, new GenerateReceiptMessage(order.Id, languageCode)),
                    cancellationToken);

                logger.LogInformation(
                    "FISCAL-RECON re-enqueued generate-receipt for order {OrderId} (key {MessageKey})",
                    order.Id, key);
                reEnqueued++;
            }
            catch (Exception ex)
            {
                // Do NOT mask a transient re-enqueue failure as done — the item stays a candidate for
                // the next tick. Acking it here would re-create the silent loss this sweep exists to
                // catch (ADR-0002 D3.3 carve-out spirit).
                logger.LogError(ex,
                    "FISCAL-RECON failed to re-enqueue generate-receipt for order {OrderId} (key {MessageKey})",
                    order.Id, key);
            }
        }

        return reEnqueued;
    }

    private async Task<int> ReconcileInvoicesAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken)
    {
        var candidates = await payPeriodRepository.GetInvoiceReconciliationCandidatesAsync(cutoff, batchSize, cancellationToken);
        if (candidates.Count == 0)
        {
            return 0;
        }

        var reEnqueued = 0;
        foreach (var item in candidates)
        {
            tenantProvider.ClearTenantOverride();
            if (!string.IsNullOrEmpty(item.TenantId))
            {
                tenantProvider.SetTenantOverride(item.TenantId);
            }

            var key = MessageKeys.Invoice(item.PayPeriodId, item.EmployeeId);

            try
            {
                // The invoice consumer is a Wave-0 stub (ADR-0002 D2.2 / GenerateInvoiceFunction) — we
                // re-enqueue ONLY so the row lands when the effect ships; we do NOT generate the invoice
                // here. LanguageCode defaults to English (the stub ignores it).
                await queueClient.SendAsync(
                    QueueNames.GenerateInvoice,
                    new QueueEnvelope<GenerateInvoiceMessage>(
                        key, item.TenantId, new GenerateInvoiceMessage(item.EmployeeId, item.PayPeriodId, Constants.Language.English)),
                    cancellationToken);

                logger.LogInformation(
                    "FISCAL-RECON re-enqueued generate-invoice for period {PayPeriodId} employee {EmployeeId} (key {MessageKey})",
                    item.PayPeriodId, item.EmployeeId, key);
                reEnqueued++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "FISCAL-RECON failed to re-enqueue generate-invoice for period {PayPeriodId} employee {EmployeeId} (key {MessageKey})",
                    item.PayPeriodId, item.EmployeeId, key);
            }
        }

        return reEnqueued;
    }

    private async Task<FiscalEnforcementMode> ResolveEnforcementModeAsync(
        Order order,
        Dictionary<string, FiscalEnforcementMode> cache,
        CancellationToken cancellationToken)
    {
        var countryId = order.CustomerAddress?.CountryId;
        if (countryId == null)
        {
            return FiscalEnforcementMode.None;
        }

        if (cache.TryGetValue(countryId, out var cached))
        {
            return cached;
        }

        var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
        var mode = countryConfig?.FiscalEnforcementMode ?? FiscalEnforcementMode.None;
        cache[countryId] = mode;
        return mode;
    }
}
