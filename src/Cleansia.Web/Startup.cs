using Cleansia.Config.Abstractions;
using Cleansia.Web.Extensions;

namespace Cleansia.Web;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "Cleansia";
    protected override string SwaggerTitle => "Cleansia.API v1";

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
    }
}
