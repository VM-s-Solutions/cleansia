namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// T-0122 (FISCAL-RECON) / ADR-0002 D3.4 + ADR-0004 C-B — the DISPATCH-layer reconciliation sweep: the
/// OUTER net for the at-most-once Wave-0 dispatch gap. Finds committed-but-unrealized fiscal work (a
/// receipt-eligible order without a fully-realized receipt; a pay period with an employee lacking an
/// invoice) older than the configured threshold and RE-ENQUEUES it through the SAME idempotent path so
/// a re-enqueue that races a successful dispatch is harmlessly deduped downstream (deterministic
/// <c>MessageKey</c> + the consumer's target-state guard).
///
/// <para>This is DISTINCT from <c>IFiscalRetryService</c> (the registration-retry layer): this sweep
/// re-enqueues the missing <i>message</i>; that one re-registers an already-claimed receipt with the
/// authority. They are not merged.</para>
/// </summary>
public interface IFiscalReconciliationService
{
    /// <summary>
    /// Runs one bounded reconciliation tick over both fiscal queues. Returns the number of messages
    /// re-enqueued (regardless of downstream dedup). Safe to run twice — two sweeps re-enqueue keys that
    /// collapse on the downstream guard, producing no duplicate effect.
    /// </summary>
    Task<int> ReconcileAsync(CancellationToken cancellationToken);
}
