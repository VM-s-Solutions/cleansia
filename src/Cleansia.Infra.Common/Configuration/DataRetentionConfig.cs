using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

/// <summary>
/// Binds the data-retention sweep's master switch from the <c>DataRetention</c> configuration section.
/// Defaults apply when the section is absent, so the sweep ships RUNNING and stays switchable — the
/// inverse of the database feature flag it replaces, which resolved an absent row to "off".
///
/// <para><b>Do NOT add a <c>DataRetention</c> block to <c>Cleansia.Functions/appsettings.json</c>.</b>
/// That file looks like the place to make the value visible, and it would silently take the kill switch
/// away. The Functions worker is the only host that runs the sweep, and it composes configuration in the
/// opposite order to the five API hosts: <c>ConfigureFunctionsWorkerDefaults</c> registers the
/// environment-variable providers first, and <c>Program.cs</c> adds <c>appsettings.json</c> last. Last
/// provider wins, so a committed JSON value BEATS the <c>DataRetention__Enabled</c> app setting an
/// operator would set in Azure — the reverse of the comment in that file, and of every other host.
/// Leaving the section absent is what keeps "off" reachable without a redeploy. <c>OutboxRetentionConfig</c>,
/// the sibling this mirrors, ships no entry there either, for the same reason.</para>
/// </summary>
public class DataRetentionConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "DataRetention"), IDataRetentionConfig
{
    public bool Enabled { get; set; } = true;
}
