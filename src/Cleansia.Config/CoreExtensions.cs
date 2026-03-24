using Cleansia.Config.Configurations;
using Cleansia.Config.Database;
using Cleansia.Config.MediatR;
using Cleansia.Config.Repositories;
using Cleansia.Config.Services;
using Cleansia.Config.Validation;
using Cleansia.Infra.Azure.Storage.Blobs;
using Cleansia.Infra.Azure.Storage.Queues;
using Cleansia.Infra.Clients.SendGrid;
using Cleansia.Infra.Clients.Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Config;

public static class CoreExtensions
{
    public static IServiceCollection AddCoreBindings(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        return services
            .AddValidators()
            .AddMediator()
            .AddServices()
            .AddRepositories()
            .AddDbContextBindings(configuration, env)
            .AddConfigurationBindings()
            .AddStripe(configuration, env)
            .AddSendGrid()
            .AddAzureBlobStorage(configuration)
            .AddAzureStorageQueues(configuration);
    }
}