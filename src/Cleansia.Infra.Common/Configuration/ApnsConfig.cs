using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class ApnsConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "APNS"), IApnsConfig
{
    public bool Enabled { get; set; }
    public string KeyId { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string CustomerBundleId { get; set; } = string.Empty;
    public bool UseSandbox { get; set; }
}
