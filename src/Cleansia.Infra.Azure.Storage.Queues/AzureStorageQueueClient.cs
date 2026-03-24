using System.Text.Json;
using Azure.Storage.Queues;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Infra.Azure.Storage.Queues;

public class AzureStorageQueueClient(QueueServiceClient queueServiceClient) : IQueueClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SendAsync<T>(string queueName, T message, CancellationToken ct = default)
    {
        var queueClient = queueServiceClient.GetQueueClient(queueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        await queueClient.SendMessageAsync(base64, cancellationToken: ct);
    }
}
