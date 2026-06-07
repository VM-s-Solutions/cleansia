using System.Text.Json;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Infra.Database;

/// <summary>
/// The durable backing for the <see cref="IPendingDispatch"/> seam. <see cref="Enqueue{T}"/> writes an
/// <see cref="OutboxMessage"/> row into the same scoped <see cref="CleansiaDbContext"/> the pipeline
/// commits, so a message row exists if and only if the business state committed — the dual-write is
/// gone, not relocated. The row's send is the drainer's job, fully decoupled from the request, so
/// <see cref="Enqueue{T}"/> stays infallible and non-network and nothing here puts bytes on the wire.
/// </summary>
public sealed class OutboxPendingDispatch(CleansiaDbContext context) : IPendingDispatch
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string TenantIdProperty = "tenantId";

    // The same in-request collapse the unique (QueueName, MessageKey) enforces at commit, applied
    // before the insert so a double-enqueue tracks one row rather than two that the DB would reject.
    private readonly HashSet<(string Queue, string Key)> _seen = [];

    public void Enqueue<T>(string queueName, T message, string messageKey)
    {
        if (!_seen.Add((queueName, messageKey)))
        {
            return;
        }

        // Serialize to the SAME camelCase wire body the queue client sends verbatim, so the drainer can
        // forward Body unchanged. The drainer has no JWT, so the tenant the row was enqueued under is
        // carried on the row; it is read back from the envelope's own tenantId field rather than added
        // out of band, keeping the row's tenant and its body in agreement.
        var body = JsonSerializer.Serialize(message, JsonOptions);
        context.OutboxMessages.Add(OutboxMessage.Create(queueName, messageKey, body, ReadTenantId(body)));
    }

    public IReadOnlyList<PendingMessage> Drain() => [];

    private static string? ReadTenantId(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty(TenantIdProperty, out var tenant)
               && tenant.ValueKind == JsonValueKind.String
            ? tenant.GetString()
            : null;
    }
}
