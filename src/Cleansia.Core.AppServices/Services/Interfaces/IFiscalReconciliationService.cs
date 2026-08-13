namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// ADR-0002 D3.4 — the OUTER net for the at-most-once dispatch gap. Finds committed-but-unrealized
/// fiscal work older than the threshold and re-enqueues it through the SAME idempotent path, so a
/// re-enqueue racing a successful dispatch is harmlessly deduped.
///
/// <para><b>DISTINCT from the retry service and not merged with it</b>: this re-enqueues the missing
/// message; that one re-registers an already-claimed receipt with the authority.
/// → /flows/cross-cutting#dead-letters</para>
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
