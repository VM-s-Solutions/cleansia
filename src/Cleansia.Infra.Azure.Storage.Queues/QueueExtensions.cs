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

        // Configure the SDK client itself to base64-encode every outgoing
        // message and base64-decode every received one. The Azure Functions
        // queue-trigger extension v5+ defaults to base64 too (see
        // host.json `extensions.queues.messageEncoding`), so consumer and
        // producer agree without any manual `Convert.ToBase64String` in
        // application code. This was previously done by hand in
        // AzureStorageQueueClient — fragile because a future SDK or
        // extension default flip would silently break.
        services.AddSingleton(_ => new QueueServiceClient(
            connectionString,
            new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }));
        services.AddSingleton<IQueueClient, AzureStorageQueueClient>();

        return services;
    }
}
