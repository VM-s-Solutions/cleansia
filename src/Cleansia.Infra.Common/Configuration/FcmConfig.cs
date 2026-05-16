using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class FcmConfig(IConfiguration configuration)
    : AutoBindConfig(configuration, "FCM"), IFcmConfig
{
    public string ServiceAccountJson { get; set; } = string.Empty;
}
