using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class GoogleConfig(IConfiguration configuration) : AutoBindConfig(configuration, "Google"), IGoogleConfig
{
    public bool IsDevelopment { get; set; } = true;
}