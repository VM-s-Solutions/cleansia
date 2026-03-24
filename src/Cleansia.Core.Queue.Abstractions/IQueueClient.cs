namespace Cleansia.Core.Queue.Abstractions;

public interface IQueueClient
{
    Task SendAsync<T>(string queueName, T message, CancellationToken ct = default);
}
