using Cleansia.Config.Abstractions;
using Cleansia.Web.Admin.Extensions;

namespace Cleansia.Web.Admin;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "CleansiaAdmin";
    protected override string SwaggerTitle => "Cleansia.Admin.API v1";

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
    }
}
