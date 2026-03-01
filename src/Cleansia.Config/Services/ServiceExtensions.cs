using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Infra.Database;
using Cleansia.Infra.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Services;

public static class ServiceExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IAppConfigurationProvider, AppConfigurationProvider>();
        services.AddInfrastructureServices();

        return services;
    }
}