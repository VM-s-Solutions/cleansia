using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Infra.Clients.SendGrid;

public static class SendGridExtensions
{
    public static IServiceCollection AddSendGrid(this IServiceCollection services)
    {
        services.AddTransient<ISendGridClientFactory, SendGridClientFactory>(provider =>
        {
            var sendGridConfig = provider.GetRequiredService<ISendGridConfig>();
            return new SendGridClientFactory(sendGridConfig);
        });

        return services;
    }
}