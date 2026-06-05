using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class GoogleConfig(IConfiguration configuration) : AutoBindConfig(configuration, "Google"), IGoogleConfig
{
    public string ClientId { get; set; } = string.Empty;
}
