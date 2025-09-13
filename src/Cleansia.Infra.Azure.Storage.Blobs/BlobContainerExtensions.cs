using Cleansia.Core.Blobs.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cleansia.Infra.Azure.Storage.Blobs;

public static class BlobContainerExtensions
{
    public static IServiceCollection AddAzureBlobStorage(this IServiceCollection services, IConfiguration configuration, IHostEnvironment env)
    {
        var config = new BlobContainerConfiguration();

        configuration.Bind(nameof(BlobContainerConfiguration), config);
        services.AddSingleton(config);

        services.AddTransient<IBlobContainerClientFactory, BlobContainerClientFactory>(provider => new BlobContainerClientFactory(configuration, config.ConnectionStringName, env.IsProduction()));

        return services;
    }
}