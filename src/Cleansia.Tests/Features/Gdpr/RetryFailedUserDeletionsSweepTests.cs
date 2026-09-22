using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
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
/// stops it; one row's throw does not stop the rest. A retry that fails or throws tells the row's own
/// company once, in a scope of its own that commits there, with the day in the subject so tomorrow's
/// failure is a second event and today's second run — which selects no candidate — is not. Real
/// repository over SQLite, the mediator and the notifier observed.
/// </summary>
public sealed class RetryFailedUserDeletionsSweepTests : IDisposable
{
    private static readonly DateTimeOffset StartOfToday = new(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly MutableClock _clock = new(DateTimeOffset.UtcNow);
    private readonly List<(string RequestId, string? Tenant)> _sent = [];
    private readonly List<(AdminEvent Event, string? AmbientTenant)> _raised = [];

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

    [Fact]
    public async Task A_Retry_That_Fails_Tells_The_Rows_Company_In_A_Scope_Of_Its_Own_And_Commits_There()
    {
        await EnsureSchemaAsync();
        await SeedAsync(Failed("req-stuck", lastAttempt: StartOfToday.AddMinutes(-1), tenant: TestTenants.Second));
        Answer(BusinessResult.Failure(new Error("userId", BusinessErrorMessage.GdprDeletionBlockedByOrder)));

        var response = await RunSweepAsync(EmptyConfiguration());

        Assert.Equal(new Response(1, 0, 1), response);
        var (raised, ambient) = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.ErasureFailed, raised.Key);
        Assert.Equal(TestTenants.Second, raised.TenantId);
        Assert.Equal(TestTenants.Second, ambient);
        Assert.Equal($"req-stuck:{Today()}", raised.Subject);
        var declared = AdminEventCatalog.Find(AdminNotificationEventCatalog.ErasureFailed).EmailArgOrder;
        Assert.Equal(declared.OrderBy(a => a), raised.Args.Keys.OrderBy(a => a));
        Assert.Equal(Today(), raised.Args["day"]);
        Assert.Equal("req-stuck", raised.Args["requestId"]);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Retry_That_Throws_Tells_The_Company_Too_And_A_Retry_That_Completes_Tells_Nobody()
    {
        await EnsureSchemaAsync();
        await SeedAsync(
            Failed("req-throws", StartOfToday.AddMinutes(-2)),
            Failed("req-completes", StartOfToday.AddMinutes(-1)));
        _mediator.Setup(m => m.Send(It.IsAny<AdminRetryUserDeletion.Command>(), It.IsAny<CancellationToken>()))
            .Returns<AdminRetryUserDeletion.Command, CancellationToken>((command, _) =>
                command.RequestId == "req-throws"
                    ? throw new InvalidOperationException("walk blew up")
                    : Task.FromResult(BusinessResult.Success()));

        var response = await RunSweepAsync(EmptyConfiguration());

        Assert.Equal(new Response(2, 1, 1), response);
        var (raised, _) = Assert.Single(_raised);
        Assert.Equal("req-throws", raised.Args["requestId"]);
    }

    /// <summary>
    /// The failure record bumps the row's UpdatedOn out of band; here the test plays that part, because
    /// the mediator is a double. The day's second run selects nothing, so nothing is raised twice; the
    /// next day's run selects the row again and raises a second event whose subject names the new day.
    /// </summary>
    [Fact]
    public async Task A_Second_Run_On_The_Day_Raises_Nothing_Because_It_Selects_Nothing_And_The_Next_Day_Raises_A_Second_Event()
    {
        await EnsureSchemaAsync();
        await SeedAsync(Failed("req-daily", StartOfToday.AddMinutes(-1)));
        Answer(BusinessResult.Failure(new Error("userId", BusinessErrorMessage.GdprDeletionBlockedByOrder)));

        var first = await RunSweepAsync(EmptyConfiguration());
        await StampAttemptAsync("req-daily", _clock.GetUtcNow());
        var sameDay = await RunSweepAsync(EmptyConfiguration());
        _clock.Advance(TimeSpan.FromDays(1));
        var nextDay = await RunSweepAsync(EmptyConfiguration());

        Assert.Equal(new Response(1, 0, 1), first);
        Assert.Equal(new Response(0, 0, 0), sameDay);
        Assert.Equal(new Response(1, 0, 1), nextDay);
        Assert.Equal(2, _raised.Count);
        var subjects = _raised.Select(r => r.Event.Subject).ToList();
        Assert.Equal(2, subjects.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal($"req-daily:{Today()}", subjects[0]);
        Assert.Equal($"req-daily:{Today(daysFromNow: 1)}", subjects[1]);
        Assert.Equal(Today(daysFromNow: 1), _raised[1].Event.Args["day"]);
    }

    private string Today(int daysFromNow = 0) =>
        new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero).AddDays(daysFromNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task StampAttemptAsync(string requestId, DateTimeOffset attemptedOn)
    {
        await using var ctx = NewContext();
        var request = await ctx.GdprRequests.IgnoreQueryFilters().SingleAsync(r => r.Id == requestId);
        request.Updated("system", attemptedOn);
        await ctx.SaveChangesAsync();
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
            new RecordingScopeFactory(_mediator.Object, _sent, _raised, _unitOfWork),
            _clock,
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
    /// tenant on, and the mediator inside it is the one observed — as is the notifier the sweep resolves
    /// in the second scope it opens to tell the company, recorded with the tenant that scope was set to.
    /// Resolving them here is what the sweep does through the real container.
    /// </summary>
    private sealed class RecordingScopeFactory(
        IMediator mediator,
        List<(string RequestId, string? Tenant)> sent,
        List<(AdminEvent Event, string? AmbientTenant)> raised,
        Mock<IUnitOfWork> unitOfWork) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            var tenantProvider = new FixedTenantProvider(null);
            var services = new ServiceCollection();
            services.AddSingleton<ITenantProvider>(tenantProvider);
            services.AddSingleton<IMediator>(new TenantStampingMediator(mediator, tenantProvider, sent));
            services.AddSingleton<IAdminNotifier>(new TenantStampingNotifier(tenantProvider, raised));
            services.AddSingleton(unitOfWork.Object);
            return services.BuildServiceProvider().CreateScope();
        }
    }

    private sealed class TenantStampingNotifier(ITenantProvider tenantProvider, List<(AdminEvent Event, string? AmbientTenant)> raised) : IAdminNotifier
    {
        public Task NotifyAsync(AdminEvent adminEvent, CancellationToken cancellationToken)
        {
            raised.Add((adminEvent, tenantProvider.GetCurrentTenantId()));
            return Task.CompletedTask;
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
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
