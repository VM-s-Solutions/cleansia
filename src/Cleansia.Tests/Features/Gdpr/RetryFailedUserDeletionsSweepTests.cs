using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// The daily retry sweep is a dispatcher: it selects the deletion requests due for another attempt and
/// sends one retry command per row in a scope of its own, with that row's tenant set as the override the
/// erasure inside will stamp from. Due means Failed and not attempted today (the last attempt is the
/// row's UpdatedOn), or Processing for longer than any live run could be; an export, a Completed row, a
/// row already attempted today and a Processing row minutes old are all left alone. The master switch
/// stops it; one row's throw does not stop the rest. Real repository over SQLite, the mediator observed.
/// </summary>
public sealed class RetryFailedUserDeletionsSweepTests : IDisposable
{
    private static readonly DateTimeOffset StartOfToday = new(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly Mock<IMediator> _mediator = new();
    private readonly List<(string RequestId, string? Tenant)> _sent = [];

    public RetryFailedUserDeletionsSweepTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task A_Failed_Request_Not_Attempted_Today_Is_Retried_In_Its_Own_Tenant()
    {
        await EnsureSchemaAsync();
        await SeedAsync(Failed("req-yesterday", lastAttempt: StartOfToday.AddMinutes(-1), tenant: TestTenants.Second));
        Answer(BusinessResult.Success());

        var response = await RunSweepAsync(EmptyConfiguration());

        Assert.Equal(new Response(1, 1, 0), response);
        Assert.Equal([("req-yesterday", TestTenants.Second)], _sent);
    }

    [Fact]
    public async Task A_Failed_Request_Already_Attempted_Today_Waits_For_Tomorrow()
    {
        await EnsureSchemaAsync();
        await SeedAsync(Failed("req-today", lastAttempt: StartOfToday.AddMinutes(1)));

        var response = await RunSweepAsync(EmptyConfiguration());

        Assert.Equal(new Response(0, 0, 0), response);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task A_Processing_Request_Is_Retried_Only_Once_No_Live_Run_Can_Still_Be_On_It()
    {
        await EnsureSchemaAsync();
        await SeedAsync(
            Processing("req-abandoned", lastAttempt: DateTimeOffset.UtcNow.AddMinutes(-45)),
            Processing("req-live", lastAttempt: DateTimeOffset.UtcNow.AddMinutes(-2)));
        Answer(BusinessResult.Success());

        var response = await RunSweepAsync(EmptyConfiguration());

        Assert.Equal(new Response(1, 1, 0), response);
        Assert.Equal("req-abandoned", Assert.Single(_sent).RequestId);
    }

    [Fact]
    public async Task Completed_Pending_And_Export_Rows_Are_Never_Retried()
    {
        await EnsureSchemaAsync();
        var longAgo = DateTimeOffset.UtcNow.AddDays(-30);
        var completed = Request("req-completed", GdprRequest.DeletionRequestType, longAgo);
        completed.MarkCompleted("admin");
        var pending = Request("req-pending", GdprRequest.DeletionRequestType, longAgo);
        var failedExport = Request("req-export", GdprAuditReasons.ExportRequestType, longAgo);
        failedExport.MarkFailed("system", "boom");
        await SeedAsync(completed, pending, failedExport);

        var response = await RunSweepAsync(EmptyConfiguration());

        Assert.Equal(new Response(0, 0, 0), response);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task A_Retry_That_Fails_Or_Throws_Is_Counted_And_The_Sweep_Carries_On()
    {
        await EnsureSchemaAsync();
        await SeedAsync(
            Failed("req-1", StartOfToday.AddMinutes(-3)),
            Failed("req-2", StartOfToday.AddMinutes(-2)),
            Failed("req-3", StartOfToday.AddMinutes(-1)));
        _mediator.Setup(m => m.Send(It.IsAny<AdminRetryUserDeletion.Command>(), It.IsAny<CancellationToken>()))
            .Returns<AdminRetryUserDeletion.Command, CancellationToken>((command, _) =>
                command.RequestId switch
                {
                    "req-1" => Task.FromResult(BusinessResult.Failure(new Error("userId", BusinessErrorMessage.GdprDeletionBlockedByOrder))),
                    "req-2" => throw new InvalidOperationException("walk blew up"),
                    _ => Task.FromResult(BusinessResult.Success()),
                });

        var response = await RunSweepAsync(EmptyConfiguration());

        Assert.Equal(new Response(3, 1, 2), response);
        Assert.Equal(["req-1", "req-2", "req-3"], _sent.Select(s => s.RequestId));
    }

    [Fact]
    public async Task The_Master_Switch_Off_Retries_Nothing()
    {
        await EnsureSchemaAsync();
        await SeedAsync(Failed("req-yesterday", StartOfToday.AddMinutes(-1)));

        var response = await RunSweepAsync(ConfigurationFrom(("DataRetention:Enabled", "false")));

        Assert.Equal(new Response(0, 0, 0), response);
        Assert.Empty(_sent);
    }

    private void Answer(BusinessResult result) =>
        _mediator.Setup(m => m.Send(It.IsAny<AdminRetryUserDeletion.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private async Task<Response> RunSweepAsync(IConfiguration configuration)
    {
        await using var ctx = NewContext();
        var handler = new RetryFailedUserDeletions.Handler(
            new GdprRequestRepository(ctx),
            new DataRetentionConfig(configuration),
            new RecordingScopeFactory(_mediator.Object, _sent),
            NullLogger<RetryFailedUserDeletions.Handler>.Instance);

        var result = await handler.Handle(new RetryFailedUserDeletions.Command(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return new Response(result.Value!.Candidates, result.Value.Completed, result.Value.Failed);
    }

    private sealed record Response(int Candidates, int Completed, int Failed);

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    private static IConfiguration ConfigurationFrom(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static GdprRequest Failed(string id, DateTimeOffset lastAttempt, string tenant = TestTenants.Default)
    {
        var request = Request(id, GdprRequest.DeletionRequestType, lastAttempt.AddDays(-1), tenant);
        request.MarkFailed("system", "DbUpdateException: boom");
        request.Updated("system", lastAttempt);
        return request;
    }

    private static GdprRequest Processing(string id, DateTimeOffset lastAttempt)
    {
        var request = Request(id, GdprRequest.DeletionRequestType, lastAttempt);
        request.MarkProcessing();
        return request;
    }

    private static GdprRequest Request(string id, string type, DateTimeOffset createdOn, string tenant = TestTenants.Default)
    {
        var request = GdprRequest.Create($"user-{id}", type);
        request.Id = id;
        request.TenantId = tenant;
        request.Created("seed", createdOn);
        return request;
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    private async Task SeedAsync(params GdprRequest[] requests)
    {
        await using var seed = NewContext();
        seed.GdprRequests.AddRange(requests);
        await seed.SaveChangesAsync();
    }

    /// <summary>
    /// Each candidate gets a scope of its own; the scope's tenant provider is what the sweep sets the row's
    /// tenant on, and the mediator inside it is the one observed. Resolving them here is what the sweep
    /// does through the real container.
    /// </summary>
    private sealed class RecordingScopeFactory(IMediator mediator, List<(string RequestId, string? Tenant)> sent) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            var tenantProvider = new FixedTenantProvider(null);
            var services = new ServiceCollection();
            services.AddSingleton<ITenantProvider>(tenantProvider);
            services.AddSingleton<IMediator>(new TenantStampingMediator(mediator, tenantProvider, sent));
            return services.BuildServiceProvider().CreateScope();
        }
    }

    private sealed class TenantStampingMediator(IMediator inner, ITenantProvider tenantProvider, List<(string RequestId, string? Tenant)> sent) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is AdminRetryUserDeletion.Command command)
            {
                sent.Add((command.RequestId, tenantProvider.GetCurrentTenantId()));
            }

            return inner.Send(request, cancellationToken);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            inner.Send(request, cancellationToken);

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => inner.Send(request, cancellationToken);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            inner.CreateStream(request, cancellationToken);

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            inner.CreateStream(request, cancellationToken);

        public Task Publish(object notification, CancellationToken cancellationToken = default) => inner.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification =>
            inner.Publish(notification, cancellationToken);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
