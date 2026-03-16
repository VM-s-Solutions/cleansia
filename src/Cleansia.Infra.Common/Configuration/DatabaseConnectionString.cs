using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Infra.Common.Configuration;

public class DatabaseConnectionString(IConfiguration configuration)
    : AutoBindConfig(configuration, "ConnectionStrings"), IDatabaseConnectionString
{
    public string DefaultConnection { get; set; } = null!;
}