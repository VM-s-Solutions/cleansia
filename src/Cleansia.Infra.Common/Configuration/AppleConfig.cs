using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class AppleConfig(IConfiguration configuration) : AutoBindConfig(configuration, "Apple"), IAppleConfig
{
    public string BundleId { get; set; } = string.Empty;
}
