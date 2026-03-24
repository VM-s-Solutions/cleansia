using Azure.Storage.Queues;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Infra.Azure.Storage.Queues;

public static class QueueExtensions
{
    public static IServiceCollection AddAzureStorageQueues(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("QueueStorageConnectionString");

        services.AddSingleton(_ => new QueueServiceClient(connectionString));
        services.AddSingleton<IQueueClient, AzureStorageQueueClient>();

        return services;
    }
}
