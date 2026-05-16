using Cleansia.Config.Abstractions;
using Cleansia.Web.Mobile.Customer.Extensions;
using Cleansia.Web.Mobile.Customer.Middleware;

namespace Cleansia.Web.Mobile.Customer;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "CleansiaMobileCustomer";
    protected override string SwaggerTitle => "Cleansia.Mobile.Customer.API v1";
    protected override Type RequestLoggingMiddlewareType => typeof(RequestLoggingMiddleware);

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
    }
}
