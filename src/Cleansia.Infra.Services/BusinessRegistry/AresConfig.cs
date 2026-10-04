using Cleansia.Infra.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Services.BusinessRegistry;

/// <summary>
/// Binds the <c>Ares</c> section. On unless a host says otherwise, so a deployment that forgets the
/// section still checks; local development and the test hosts switch it off.
/// </summary>
public sealed class AresConfig(IConfiguration configuration) : AutoBindConfig(configuration, "Ares")
{
    public bool Enabled { get; set; } = true;
}
