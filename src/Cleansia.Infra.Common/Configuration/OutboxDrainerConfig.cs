using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

/// <summary>
/// Binds the drainer tunables from the <c>OutboxDrainer</c> configuration section. Defaults apply when
/// the section is absent, so the drainer ships working and the cadence stays tunable.
/// </summary>
public class OutboxDrainerConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "OutboxDrainer"), IOutboxDrainerConfig
{
    public int BatchSize { get; set; } = 100;
    public int MaxAttempts { get; set; } = 10;
    public int BaseBackoffSeconds { get; set; } = 30;
    public int LeaseSeconds { get; set; } = 120;
}
