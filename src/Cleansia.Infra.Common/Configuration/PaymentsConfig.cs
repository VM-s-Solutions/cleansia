using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

/// <summary>
/// Binds the <c>Payments</c> section. Every switch here defaults OFF, so a host with no section charges
/// nothing it was not told to.
///
/// <para><b>Do NOT add a <c>Payments</c> block to <c>Cleansia.Functions/appsettings.json</c>.</b> The
/// Functions worker is the host that runs the off-session charge sweep, and it adds that file after the
/// environment variables, so a committed value there beats the <c>Payments__OffSessionChargesEnabled</c>
/// app setting and switching the charges off again would need a redeploy — see
/// <see cref="DataRetentionConfig"/>, which carries the same rule for the same reason.</para>
/// </summary>
public class PaymentsConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "Payments"), IPaymentsConfig
{
    public bool OffSessionChargesEnabled { get; set; }
}
