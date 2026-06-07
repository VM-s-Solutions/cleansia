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

        // The IPendingDispatch seam is SCOPED (per request): a command handler records intent on the
        // request's instance; PostCommitDispatchBehavior gates it on the commit. The durable backing
        // (OutboxPendingDispatch — it writes an outbox row into the pipeline's DbContext) is registered
        // in AddRepositories, which runs after this, because the implementation needs the scoped
        // DbContext; the command-handler call sites are unchanged either way.

        // The claim-then-act dedup for non-transactional consumer effects with no domain target-state
        // (the send-email consumer). Singleton so a claim survives redeliveries within the worker
        // process; Wave-1 swaps the backing for a durable ProcessedMessage row.
        services.AddSingleton<IIdempotencyGuard, InMemoryIdempotencyGuard>();

        return services;
    }
}
