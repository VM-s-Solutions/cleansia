using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Cleansia.Config.Health;
using Cleansia.Config.Services;
using Cleansia.Infra.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The Functions host's queue-storage probe under managed identity (E-4). The host's identity holds
/// Storage Queue Data Contributor and no management role on the account, so reading the queue service's
/// properties answers 403 — a probe built on it would report storage down on a host whose every trigger
/// works, and App Service would keep recycling that host.
/// </summary>
public sealed class FunctionsHealthCheckQueueProbeTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FunctionsHealthCheckQueueProbeTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private async Task<HealthProbe> QueueProbe(Mock<QueueServiceClient> queues)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options;

        var report = await new FunctionsHealthCheck(options, queues.Object, NullLogger<FunctionsHealthCheck>.Instance)
            .CheckAsync(CancellationToken.None);

        return Assert.Single(report.Probes, probe => probe.Name == "queue-storage");
    }

    [Fact]
    public async Task Passes_with_only_the_queue_data_role()
    {
        var queues = new Mock<QueueServiceClient>(MockBehavior.Strict);
        queues.Setup(q => q.GetQueuesAsync(It.IsAny<QueueTraits>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<QueueItem>.FromPages([Page<QueueItem>.FromValues([], null, Mock.Of<Response>())]));
        queues.Setup(q => q.GetPropertiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "This request is not authorized to perform this operation using this permission."));

        var probe = await QueueProbe(queues);

        Assert.True(probe.Ok, probe.Detail);
    }

    [Fact]
    public async Task Still_fails_when_the_queue_service_is_unreachable()
    {
        var queues = new Mock<QueueServiceClient>(MockBehavior.Strict);
        queues.Setup(q => q.GetQueuesAsync(It.IsAny<QueueTraits>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new RequestFailedException("No such host is known."));

        var probe = await QueueProbe(queues);

        Assert.False(probe.Ok);
        Assert.Equal(nameof(RequestFailedException), probe.Detail);
    }

    [Fact]
    public async Task Answers_unhealthy_within_the_bound_when_the_queue_service_never_answers()
    {
        var queues = new Mock<QueueServiceClient>(MockBehavior.Strict);
        queues.Setup(q => q.GetQueuesAsync(It.IsAny<QueueTraits>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((QueueTraits _, string? _, CancellationToken ct) => new NeverAnsweringPageable(ct));

        var probe = await QueueProbe(queues).WaitAsync(ReadinessHealthChecks.ReadinessCheckTimeout * 3);

        Assert.False(probe.Ok);
        Assert.Equal(nameof(TaskCanceledException), probe.Detail);
    }

    private sealed class NeverAnsweringPageable(CancellationToken cancellationToken)
        : AsyncPageable<QueueItem>(cancellationToken)
    {
        public override async IAsyncEnumerable<Page<QueueItem>> AsPages(
            string? continuationToken = null,
            int? pageSizeHint = null)
        {
            await Task.Delay(Timeout.Infinite, CancellationToken);
            yield break;
        }
    }
}
