using Cleansia.Core.Blobs.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Infra.Azure.Storage.Blobs;

public static class BlobContainerExtensions
{
    public static IServiceCollection AddAzureBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var config = new BlobContainerConfiguration();
        configuration.Bind(nameof(BlobContainerConfiguration), config);
        services.AddSingleton(config);

        services.AddTransient<IBlobContainerClientFactory>(provider =>
            new BlobContainerClientFactory(configuration, config));

        return services;
    }
}
