using System.Text.Json;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit.Abstractions;

namespace Cleansia.IntegrationTests.Features.Refunds;

[Collection("PostgresCollection")]
public sealed class RedrivePendingRefundIsolationTests(PostgresContainerFixture fixture, ITestOutputHelper output) : IAsyncLifetime
{
    private const string OrderId = "ord-redrive-isolation";
    private const string RefundA = "refund-isolation-a";
    private const string RefundB = "refund-isolation-b";
    private NpgsqlDataSource _dataSource = default!;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlDataSourceBuilder(new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        { Database = "redrive_isolation_test" }.ConnectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();
        await using var ctx = NewContext(new ScopeTenantProvider());
        await ctx.Database.EnsureDeletedAsync();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ReloadTypesAsync();
        await using var command = new NpgsqlCommand("""
            DO $$ DECLARE r record; BEGIN
              FOR r IN SELECT conname, conrelid::regclass AS tbl FROM pg_constraint
                       WHERE contype = 'f' AND conrelid = '"Orders"'::regclass
              LOOP EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname); END LOOP;
            END $$;
            """, connection);
        await command.ExecuteNonQueryAsync();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = "cz";
        ctx.Countries.Add(country);
        await ctx.CommitAsync(CancellationToken.None);
        var tenant = new ScopeTenantProvider();
        tenant.SetTenantOverride(TestTenants.Default);
        await using var seed = NewContext(tenant);
        var admin = User.CreateWithPassword("redrive-admin@cleansia.test", "Password123!", "Rita", "Refund",
            UserProfile.Administrator, adminRole: AdminRole.Administrator);
        admin.Id = "redrive-admin";
        admin.ConfirmEmail();
        seed.Users.Add(admin);
        var order = Order.Create("Rita Refund", "refund@cleansia.test", "+420000000000",
            Address.Create("Test St 1", "Brno", "60200", "cz"), 1, 1, DateTime.UtcNow.AddDays(-2),
            PaymentType.Card, 1000m, "czk", PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        seed.Orders.Add(order);
        var a = Refund.Create(OrderId, $"refund:{OrderId}:admin:a", 250m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
        a.Id = RefundA;
        a.Created("seed", DateTimeOffset.UtcNow.AddHours(-30));
        var b = Refund.Create(OrderId, $"refund:{OrderId}:admin:b", 500m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
        b.Id = RefundB;
        b.Created("seed", DateTimeOffset.UtcNow.AddHours(-29));
        seed.Refunds.AddRange(a, b);
        await seed.CommitAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext(new ScopeTenantProvider())) await ctx.Database.EnsureDeletedAsync();
        await _dataSource.DisposeAsync();
    }

    [Fact]
    public async Task A_Refused_Alert_Commit_Leaves_Only_B_Durable_And_The_Fault_Repeats_Safely()
    {
        var first = await RunAsync();
        Assert.Equal(1, first.Response.Alerted);
        Assert.Equal(1, first.Faults);
        await AssertPersistedAsync();
        await using (var reset = NewContext(new ScopeTenantProvider()))
        {
            await reset.Set<UserNotification>().IgnoreQueryFilters().ExecuteDeleteAsync();
            await reset.OutboxMessages.IgnoreQueryFilters().ExecuteDeleteAsync();
        }
        var second = await RunAsync();
        Assert.Equal(1, second.Response.Alerted);
        Assert.Equal(1, second.Faults);
        await AssertPersistedAsync();
    }

    [Fact]
    public async Task A_Refused_Alert_Commit_Uses_A_Different_Context_And_Dispatch_Buffer_From_B()
    {
        var run = await RunAsync();
        await AssertPersistedAsync();
        output.WriteLine($"contexts={run.Contexts.Count}; distinct={run.Contexts.Distinct().Count()}; refused commits={run.Faults}");
        var a = Assert.Single(run.Events, e => e.Kind == "a-save-refused").ContextId;
        var b = Assert.Single(run.Events, e => e.Kind == "b-alert-save-begins").ContextId;
        Assert.NotSame(Assert.Single(run.Dispatches, d => d.ContextId == a).Instance,
            Assert.Single(run.Dispatches, d => d.ContextId == b).Instance);
        Assert.Equal(3, run.Contexts.Distinct().Count());
        var refused = run.Events.FindIndex(e => e.Kind == "a-save-refused");
        var disposed = run.Events.FindIndex(e => e.Kind == "context-disposed" && e.ContextId == a);
        Assert.True(disposed > refused, "A must finish asynchronous context disposal after its save refusal.");
        Assert.True(disposed < run.Events.FindIndex(e => e.Kind == "context-resolved" && e.ContextId == b),
            "A must finish asynchronous context disposal before B resolves its context.");
        Assert.True(disposed < run.Events.FindIndex(e => e.Kind == "dispatch-resolved" && e.ContextId == b),
            "A must finish asynchronous context disposal before B resolves its dispatch buffer.");
        Assert.True(disposed < run.Events.FindIndex(e => e.Kind == "b-alert-save-begins"),
            "A must finish asynchronous context disposal before B's alert save begins.");
    }

    private async Task AssertPersistedAsync()
    {
        await using var read = NewContext(new ScopeTenantProvider());
        var refunds = await read.Refunds.IgnoreQueryFilters().AsNoTracking().OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(new[] { 250m, 500m }, refunds.Select(r => r.Amount));
        Assert.All(refunds, r => Assert.Equal(RefundStatus.Pending, r.Status));
        Assert.Equal(PaymentStatus.Paid, (await read.Orders.IgnoreQueryFilters().SingleAsync()).PaymentStatus);
        var notification = Assert.Single(await read.Set<UserNotification>().IgnoreQueryFilters().AsNoTracking().ToListAsync());
        Assert.Equal(AdminNotificationEventCatalog.RefundNeedsRetry, notification.EventKey);
        Assert.Equal(TestTenants.Default, notification.TenantId);
        var intent = Assert.Single(await read.OutboxMessages.IgnoreQueryFilters().AsNoTracking().ToListAsync());
        Assert.Contains(RefundB, intent.MessageKey);
        Assert.DoesNotContain(RefundA, intent.MessageKey);
        Assert.Empty(await read.CreditAccounts.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await read.CreditTransactions.IgnoreQueryFilters().ToListAsync());
        output.WriteLine($"refunds={string.Join(',', refunds.Select(r => $"{r.Id}:{r.Status}:{r.Amount}"))}; payment=Paid; feed=1; outbox={intent.MessageKey}; credit accounts/ledger=0/0");
    }

    private async Task<(RedrivePendingRefunds.Response Response, List<Guid> Contexts,
        List<(Guid ContextId, OutboxPendingDispatch Instance)> Dispatches,
        List<(string Kind, Guid ContextId)> Events, int Faults)> RunAsync()
    {
        var contexts = new List<Guid>();
        var dispatches = new List<(Guid ContextId, OutboxPendingDispatch Instance)>();
        var events = new List<(string Kind, Guid ContextId)>();
        var fault = new RefuseAAlert(events);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<RedrivePendingRefunds>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkPipelineBehavior<,>));
        services.AddScoped<ITenantProvider>(_ => new ScopeTenantProvider());
        services.AddScoped<CleansiaDbContext>(sp =>
        {
            var ctx = new DisposalRecordingContext(new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(_dataSource).AddInterceptors(fault).Options,
                sp.GetRequiredService<ITenantProvider>(), events);
            contexts.Add(ctx.ContextId.InstanceId);
            events.Add(("context-resolved", ctx.ContextId.InstanceId));
            return ctx;
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CleansiaDbContext>());
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPendingDispatch>(sp =>
        {
            var ctx = sp.GetRequiredService<CleansiaDbContext>();
            var dispatch = new OutboxPendingDispatch(ctx);
            dispatches.Add((ctx.ContextId.InstanceId, dispatch));
            events.Add(("dispatch-resolved", ctx.ContextId.InstanceId));
            return dispatch;
        });
        services.AddScoped<INotificationProducer, NotificationProducer>();
        var config = new Mock<IAppConfigurationProvider>();
        services.AddSingleton(config.Object);
        services.AddScoped<IAdminNotifier, AdminNotifier>();
        services.AddScoped<IRefundService>(_ => Mock.Of<IRefundService>());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RedrivePendingRefunds.Command());
        Assert.True(result.IsSuccess);
        for (var i = 0; i < events.Count; i++)
            output.WriteLine("scope-event=" + JsonSerializer.Serialize(new
                { sequence = i, kind = events[i].Kind, contextId = events[i].ContextId }));
        for (var i = 0; i < dispatches.Count; i++)
            output.WriteLine("pending-dispatch=" + JsonSerializer.Serialize(new
                { instance = i, contextId = dispatches[i].ContextId }));
        return (result.Value!, contexts, dispatches, events.ToList(), fault.Count);
    }

    private sealed class DisposalRecordingContext(DbContextOptions<CleansiaDbContext> options,
        ITenantProvider tenant, List<(string Kind, Guid ContextId)> events) : CleansiaDbContext(options,
            new TestUserSessionProvider("system", "system@cleansia.test"), tenant)
    {
        public override async ValueTask DisposeAsync()
        {
            var id = ContextId.InstanceId;
            await base.DisposeAsync();
            events.Add(("context-disposed", id));
        }
    }

    private sealed class RefuseAAlert(List<(string Kind, Guid ContextId)> events) : SaveChangesInterceptor
    {
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<Cleansia.Core.Domain.Outbox.OutboxMessage>()
                .Any(e => e.State == EntityState.Added && e.Entity.MessageKey.Contains(RefundA, StringComparison.Ordinal)))
            {
                Count++;
                events.Add(("a-save-refused", data.Context.ContextId.InstanceId));
                throw new InvalidOperationException("Refused A alert after feed/outbox staging");
            }
            if (data.Context.ChangeTracker.Entries<Cleansia.Core.Domain.Outbox.OutboxMessage>()
                .Any(e => e.State == EntityState.Added && e.Entity.MessageKey.Contains(RefundB, StringComparison.Ordinal)))
                events.Add(("b-alert-save-begins", data.Context.ContextId.InstanceId));
            return ValueTask.FromResult(result);
        }
    }

    private CleansiaDbContext NewContext(ITenantProvider tenant, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).AddInterceptors(interceptors).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"), tenant);

    private sealed class ScopeTenantProvider : ITenantProvider
    {
        private string? _tenant;
        public string? GetCurrentTenantId() => _tenant;
        public void SetTenantOverride(string tenant) => _tenant = tenant;
        public void ClearTenantOverride() => _tenant = null;
    }
}
