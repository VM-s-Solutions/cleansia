using Cleansia.Config.Configurations;
using Cleansia.Config.Database;
using Cleansia.Config.MediatR;
using Cleansia.Config.Repositories;
using Cleansia.Config.Services;
using Cleansia.Config.Validation;
using Cleansia.Infra.Azure.Storage.Blobs;
using Cleansia.Infra.Azure.Storage.Queues;
using Cleansia.Infra.Clients.Apns;
using Cleansia.Infra.Clients.Fcm;
using Cleansia.Infra.Clients.SendGrid;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Fiscal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Config;

public static class CoreExtensions
{
    /// <param name="eagerlyReloadNpgsqlTypeCatalog">
    /// Functions-worker only — see <see cref="Database.DbContextBindingExtensions.AddDbContextBindings"/>.
    /// Leaving it false keeps a blocking Postgres round-trip out of every API host's startup path.
    /// </param>
    public static IServiceCollection AddCoreBindings(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env,
        bool eagerlyReloadNpgsqlTypeCatalog = false)
    {
        return services
            .AddValidators()
            .AddMediator()
            .AddServices()
            .AddRepositories()
            .AddDbContextBindings(configuration, env, eagerlyReloadNpgsqlTypeCatalog)
            .AddConfigurationBindings()
            .AddStripe(configuration, env)
            .AddSendGrid()
            .AddFcm()
            .AddApns()
            .AddAzureBlobStorage(configuration)
            .AddAzureStorageQueues(configuration)
            .AddFiscalServices(configuration);
    }
}