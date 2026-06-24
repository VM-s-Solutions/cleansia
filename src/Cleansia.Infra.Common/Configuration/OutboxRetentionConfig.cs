using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

/// <summary>
/// Binds the outbox retention-prune tunables from the <c>OutboxRetention</c> configuration section. Defaults
/// apply when the section is absent, so the prune ships working with a sane window and stays tunable.
/// </summary>
public class OutboxRetentionConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "OutboxRetention"), IOutboxRetentionConfig
{
    public bool Enabled { get; set; } = true;
    public int DispatchedRetentionDays { get; set; } = 14;
    public int ProcessedRetentionDays { get; set; } = 14;
    public int BatchSize { get; set; } = 500;
}
