using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
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

namespace Cleansia.IntegrationTests.Features.Bookings;

[Collection("PostgresCollection")]
public sealed class RecurringReminderIsolationTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private const string OrderA = "ord-reminder-a";
    private const string OrderB = "ord-reminder-b";
    private NpgsqlDataSource _dataSource = default!;

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        { Database = "recurring_reminder_isolation_test" }.ConnectionString;
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();
        await using var bootstrap = NewContext(new ScopeTenantProvider());
        await bootstrap.Database.EnsureDeletedAsync();
        await TestTenants.EnsureCreatedWithRegistryAsync(bootstrap);
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ReloadTypesAsync();
        await using var cmd = new NpgsqlCommand("""
            DO $$ DECLARE r record; BEGIN
              FOR r IN SELECT conname, conrelid::regclass AS tbl FROM pg_constraint
                       WHERE contype = 'f' AND conrelid = '"Orders"'::regclass
              LOOP EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname); END LOOP;
            END $$;
            """, conn);
        await cmd.ExecuteNonQueryAsync();
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = "cz";
        bootstrap.Countries.Add(country);
        bootstrap.Languages.Add(Language.Create("en", "English"));
        await bootstrap.CommitAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext(new ScopeTenantProvider())) await ctx.Database.EnsureDeletedAsync();
        await _dataSource.DisposeAsync();
    }

    [Fact]
    public async Task A_Payment_Refusing_One_Reminder_Holds_Back_No_Other_Company()
    {
        await SeedAsync(OrderA, TestTenants.Default, OrderStatus.New);
        await SeedAsync(OrderB, TestTenants.Second, OrderStatus.New);
        var race = new PayAtReminderSave(PayAAsync);

        var response = await RunSweepAsync(race);

        Assert.True(race.Ran);
        Assert.Equal(1, response.RemindersSent);
        await using var read = NewContext(new ScopeTenantProvider());
        var orders = await read.Orders.IgnoreQueryFilters().OrderBy(o => o.Id).ToListAsync();
        Assert.Null(orders[0].RecurringReminderSentAt);
        Assert.Equal(PaymentStatus.Paid, orders[0].PaymentStatus);
        Assert.NotNull(orders[1].RecurringReminderSentAt);
        var intent = Assert.Single(await read.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(MessageKeys.Push("user-" + OrderB, NotificationEventCatalog.RecurringScheduled, OrderB), intent.MessageKey);
        Assert.Equal(TestTenants.Second, intent.TenantId);
        var feed = Assert.Single(await read.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
        Assert.Equal(TestTenants.Second, feed.TenantId);
        Assert.Equal(0, (await RunSweepAsync()).RemindersSent);
        Assert.Equal(1, await read.OutboxMessages.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task A_Repeated_Item_Fault_Is_Counted_Without_Holding_Back_Another_Reminder()
    {
        await SeedAsync(OrderA, TestTenants.Default, OrderStatus.New);
        await SeedAsync(OrderB, TestTenants.Second, OrderStatus.New);
        var first = await RunSweepAsync(new RefuseASave());
        Assert.Equal(new SendRecurringOrderReminders.Response(1, 2, 1), first);
        var second = await RunSweepAsync(new RefuseASave());
        Assert.Equal(new SendRecurringOrderReminders.Response(0, 1, 1), second);
    }

    [Fact]
    public async Task Host_Cancellation_Is_Rethrown_And_Leaves_No_Reminder_Intent()
    {
        await SeedAsync(OrderA, TestTenants.Default, OrderStatus.New);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunSweepAsync(cancellation.Token,
            new CancelAtSave(cancellation)));
        await using var read = NewContext(new ScopeTenantProvider());
        Assert.Empty(await read.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Null((await read.Orders.IgnoreQueryFilters().SingleAsync()).RecurringReminderSentAt);
    }

    private sealed class RefuseASave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<Order>().Any(e => e.Entity.Id == OrderA && e.State == EntityState.Modified))
                throw new InvalidOperationException("Refused reminder A");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CancelAtSave(CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        { cancellation.Cancel(); ct.ThrowIfCancellationRequested(); return ValueTask.FromResult(result); }
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed)]
    public async Task A_Terminal_Unpaid_Occurrence_Is_Never_Reminded(OrderStatus status)
    {
        await SeedAsync(OrderA, TestTenants.Default, status);

        Assert.Equal(0, (await RunSweepAsync()).RemindersSent);

        await using var read = NewContext(new ScopeTenantProvider());
        Assert.Null((await read.Orders.IgnoreQueryFilters().SingleAsync()).RecurringReminderSentAt);
        Assert.Empty(await read.OutboxMessages.IgnoreQueryFilters().ToListAsync());
    }

    private async Task SeedAsync(string id, string tenantId, OrderStatus status)
    {
        var tenant = new ScopeTenantProvider();
        tenant.SetTenantOverride(tenantId);
        await using var ctx = NewContext(tenant);
        var user = User.CreateWithPassword("user-" + id + "@cleansia.test", "Password123!", "Rita", "Recurring");
        user.Id = "user-" + id;
        ctx.Users.Add(user);
        var order = Order.Create("Rita Recurring", user.Email, "+420000000000",
            Address.Create("Test St 1", "Brno", "60200", "cz"), 1, 1, DateTime.UtcNow.AddHours(12),
            PaymentType.Card, 1000m, "czk", PaymentStatus.Pending, userId: user.Id,
            recurringTemplateId: "tmpl-" + id, cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = id;
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task PayAAsync()
    {
        var tenant = new ScopeTenantProvider();
        tenant.SetTenantOverride(TestTenants.Default);
        await using var ctx = NewContext(tenant);
        (await ctx.Orders.SingleAsync(o => o.Id == OrderA)).UpdatePaymentStatus(PaymentStatus.Paid);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private Task<SendRecurringOrderReminders.Response> RunSweepAsync(params IInterceptor[] interceptors) =>
        RunSweepAsync(CancellationToken.None, interceptors);

    private async Task<SendRecurringOrderReminders.Response> RunSweepAsync(CancellationToken cancellationToken, params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantProvider>(_ => new ScopeTenantProvider());
        services.AddScoped(sp => NewContext(sp.GetRequiredService<ITenantProvider>(), interceptors));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CleansiaDbContext>());
        services.AddScoped<IOrderRepository>(sp => new OrderRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<INotificationProducer>(sp =>
        {
            var ctx = sp.GetRequiredService<CleansiaDbContext>();
            return new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx),
                new UserRepository(ctx), NullLogger<NotificationProducer>.Instance);
        });
        services.AddScoped<SendRecurringOrderReminder.Handler>();
        services.AddScoped<IMediator>(sp =>
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<SendRecurringOrderReminder.Command>(), It.IsAny<CancellationToken>()))
                .Returns((SendRecurringOrderReminder.Command command, CancellationToken ct) =>
                    new UnitOfWorkPipelineBehavior<SendRecurringOrderReminder.Command, BusinessResult<SendRecurringOrderReminder.Response>>(
                        sp.GetRequiredService<IUnitOfWork>()).Handle(command,
                        token => sp.GetRequiredService<SendRecurringOrderReminder.Handler>().Handle(command, token), ct));
            return mediator.Object;
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<SendRecurringOrderReminders.Handler>(scope.ServiceProvider);
        var command = new SendRecurringOrderReminders.Command();
        var result = await new UnitOfWorkPipelineBehavior<SendRecurringOrderReminders.Command,
            BusinessResult<SendRecurringOrderReminders.Response>>(scope.ServiceProvider.GetRequiredService<IUnitOfWork>())
            .Handle(command, ct => handler.Handle(command, ct), cancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private CleansiaDbContext NewContext(ITenantProvider tenant, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).AddInterceptors(interceptors).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"), tenant);

    private sealed class PayAtReminderSave(Func<Task> pay) : SaveChangesInterceptor
    {
        public bool Ran { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!Ran && data.Context!.ChangeTracker.Entries<Order>().Any(e => e.Entity.Id == OrderA &&
                e.State == EntityState.Modified && e.Entity.RecurringReminderSentAt != null))
            { Ran = true; await pay(); }
            return result;
        }
    }

    private sealed class ScopeTenantProvider : ITenantProvider
    {
        private string? _tenant;
        public string? GetCurrentTenantId() => _tenant;
        public void SetTenantOverride(string tenant) => _tenant = tenant;
        public void ClearTenantOverride() => _tenant = null;
    }
}
