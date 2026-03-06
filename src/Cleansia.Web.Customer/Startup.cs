using Cleansia.Config.Abstractions;
using Cleansia.Web.Customer.Extensions;

namespace Cleansia.Web.Customer;

public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    : CleansiaStartupBase(configuration, environment)
{
    protected override string CorsPolicyName => "CleansiaCustomer";
    protected override string SwaggerTitle => "Cleansia.Customer.API v1";

    protected override void AddProjectServices(IServiceCollection services)
    {
        services.AddServices(Configuration, Environment);
    }
}
