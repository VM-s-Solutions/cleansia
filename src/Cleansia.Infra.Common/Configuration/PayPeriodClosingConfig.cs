using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

/// <summary>
/// Binds the nightly pay-period job's master switch from the <c>PayPeriodClosing</c> configuration
/// section. Defaults apply when the section is absent, so the job ships RUNNING and stays switchable.
/// </summary>
public class PayPeriodClosingConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "PayPeriodClosing"), IPayPeriodClosingConfig
{
    public bool Enabled { get; set; } = true;
}
