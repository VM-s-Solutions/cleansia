using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class AutoBindConfig
{
    public AutoBindConfig(IConfiguration configuration, string configurationName)
    {
        configuration.GetSection(configurationName).Bind(this);
    }
}