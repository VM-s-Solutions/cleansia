using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

/// <summary>
/// ADR-0002 D3.4 — binds the reconciliation-sweep tunables from the
/// <c>FiscalReconciliation</c> configuration section. Defaults (15 min / batch 50) apply when the
/// section is absent, so the sweep ships with the ADR's defaults and the cadence stays tunable.
/// </summary>
public class FiscalReconciliationConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "FiscalReconciliation"), IFiscalReconciliationConfig
{
    public int ThresholdMinutes { get; set; } = 15;
    public int BatchSize { get; set; } = 50;
}
