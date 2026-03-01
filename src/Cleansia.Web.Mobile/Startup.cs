using Cleansia.Config.Abstractions;
using Cleansia.Web.Mobile.Extensions;

namespace Cleansia.Web.Mobile;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "CleansiaMobile";
    protected override string SwaggerTitle => "Cleansia.Mobile.API v1";

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
    }
}
