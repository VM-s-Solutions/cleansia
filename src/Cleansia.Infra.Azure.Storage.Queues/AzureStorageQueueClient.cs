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

        // Send the JSON as-is. The QueueServiceClient is configured with
        // `MessageEncoding = Base64` (see QueueExtensions) so the SDK
        // base64-encodes on the wire and the Functions queue trigger
        // base64-decodes on the way in. Doing it both at the application
        // layer AND letting the SDK do it again was the previous bug
        // surface — when the encoding setting flips, things break silently.
        var json = JsonSerializer.Serialize(message, JsonOptions);
        await queueClient.SendMessageAsync(json, cancellationToken: ct);
    }
}
