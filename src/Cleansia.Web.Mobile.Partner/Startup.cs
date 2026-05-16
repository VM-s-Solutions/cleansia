using Cleansia.Config.Abstractions;
using Cleansia.Web.Mobile.Partner.Extensions;
using Cleansia.Web.Mobile.Partner.Middleware;

namespace Cleansia.Web.Mobile.Partner;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "CleansiaMobilePartner";
    protected override string SwaggerTitle => "Cleansia.Mobile.Partner.API v1";
    protected override Type RequestLoggingMiddlewareType => typeof(RequestLoggingMiddleware);

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
    }
}
