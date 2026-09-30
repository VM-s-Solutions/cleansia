using Azure.Storage.Queues;
using Cleansia.Infra.Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// How a host reaches the Storage Queues. DEV and local keep the account-key connection string. Prod
/// (storageManagedIdentityEnabled, E-4) refuses shared keys on the account and sets
/// <c>QueueStorageConnectionString__queueServiceUri</c> — the setting the Functions host binds its queue
/// triggers from — so a sender that still signed with a key there would fail every enqueue with a 403.
/// </summary>
public class QueueStorageManagedIdentityTests
{
    private const string ServiceUri = "https://stcleansiaweuprod.queue.core.windows.net/";

    private static QueueServiceClient Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection().AddAzureStorageQueues(configuration).BuildServiceProvider();
        return provider.GetRequiredService<QueueServiceClient>();
    }

    [Fact]
    public void The_service_uri_builds_a_client_that_holds_no_account_key()
    {
        var client = Resolve(new() { ["QueueStorageConnectionString:queueServiceUri"] = ServiceUri });

        Assert.Equal(new Uri(ServiceUri), client.Uri);
        Assert.Equal("stcleansiaweuprod", client.AccountName);
        Assert.False(client.CanGenerateAccountSasUri);
    }

    /// <summary>
    /// The API hosts' appsettings.json carries <c>UseDevelopmentStorage=true</c> under the connection
    /// string name, and the managed-identity template removes only the app setting that overrode it — so
    /// the service URI has to win, or a prod API host would enqueue into an emulator that is not there.
    /// </summary>
    [Fact]
    public void The_service_uri_wins_over_a_committed_connection_string()
    {
        var client = Resolve(new()
        {
            ["ConnectionStrings:QueueStorageConnectionString"] = "UseDevelopmentStorage=true",
            ["QueueStorageConnectionString:queueServiceUri"] = ServiceUri,
        });

        Assert.Equal(new Uri(ServiceUri), client.Uri);
        Assert.False(client.CanGenerateAccountSasUri);
    }

    [Fact]
    public void Without_it_the_connection_string_keeps_the_key_path()
    {
        var client = Resolve(new() { ["ConnectionStrings:QueueStorageConnectionString"] = "UseDevelopmentStorage=true" });

        Assert.Equal("devstoreaccount1", client.AccountName);
        Assert.True(client.CanGenerateAccountSasUri);
    }
}
