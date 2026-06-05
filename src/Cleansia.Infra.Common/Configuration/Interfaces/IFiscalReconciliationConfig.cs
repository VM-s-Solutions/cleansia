namespace Cleansia.Infra.Common.Configuration.Interfaces;

/// <summary>
/// T-0122 (FISCAL-RECON) / ADR-0002 D3.4 — tunables for the dispatch reconciliation sweep. The cadence
/// + window are a <b>tunable, not a business decision</b> (ADR-0002 §Escalations), so they are read from
/// configuration (section <c>FiscalReconciliation</c>) rather than hardcoded.
/// </summary>
public interface IFiscalReconciliationConfig
{
    /// <summary>
    /// How old (in minutes) a committed-but-unrealized fiscal artifact must be before the sweep
    /// re-enqueues it. Defaults to <b>15</b> per ADR-0002 D3.4. Orders/periods WITHIN this window
    /// (recently committed — their normal post-commit dispatch may still be on the wire) are NOT swept.
    /// </summary>
    int ThresholdMinutes { get; set; }

    /// <summary>
    /// Max items re-enqueued per queue per tick. Keeps each run bounded (in the spirit of
    /// <c>FiscalRetryService.BatchSize = 50</c>). Defaults to <b>50</b>.
    /// </summary>
    int BatchSize { get; set; }
}
