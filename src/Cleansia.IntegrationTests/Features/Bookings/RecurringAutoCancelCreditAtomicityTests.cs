using System.Data.Common;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
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

/// <summary>
/// An unconfirmed recurring occurrence on which 300 of credit was spent reaches its cutoff, and the commit that
/// would cancel it is refused. The credit comes back with the cancellation or not at all: the occurrence stays
/// open with its credit still spent, the run reports it as failed, and the next run cancels it and returns the
/// 300 once. Run on the sweep's real unit-of-work pipeline, whose own commit after the sweep is part of what is
/// under test.
/// </summary>
[Collection("PostgresCollection")]
public sealed class RecurringAutoCancelCreditAtomicityTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private const string CountryId = "country-cz-rac";
    private const string CurrencyId = "currency-czk-rac";
    private const string UserId = "user-recurring-credit";
    private const string OrderId = "ord-recurring-credit";
    private const string ReturnKey = $"credit-return:order-ended-unpaid:{OrderId}";
    private const decimal Credit = 300m;

    private NpgsqlDataSource _dataSource = default!;
    private readonly MutableTenantProvider _tenantProvider = new();

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            Database = "recurring_auto_cancel_credit_test"
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using (var bootstrap = NewContext())
        {
            await bootstrap.Database.EnsureDeletedAsync();
            await TestTenants.EnsureCreatedWithRegistryAsync(bootstrap);
        }

        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ReloadTypesAsync();
        await DropOrderForeignKeysAsync(conn);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureDeletedAsync();
        }

        await _dataSource.DisposeAsync();
    }

    [Fact]
    public async Task A_Refused_Cancellation_Returns_No_Credit_And_The_Next_Run_Returns_It_Once()
    {
        var first = await RunSweepAsync(new RefuseFirstSave());

        await using (var afterFailure = NewContext())
        {
            var order = await afterFailure.Orders.IgnoreQueryFilters()
                .Include(o => o.OrderStatusHistory)
                .SingleAsync(o => o.Id == OrderId);
            Assert.DoesNotContain(order.OrderStatusHistory, t => t.Status == OrderStatus.Cancelled);
            Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
            Assert.False(await afterFailure.CreditTransactions.IgnoreQueryFilters()
                .AnyAsync(t => t.IdempotencyKey == ReturnKey));
            Assert.False(await afterFailure.CreditAccounts.IgnoreQueryFilters().AnyAsync(a => a.UserId == UserId));
        }

        Assert.Equal(new AutoCancelStaleRecurringOrders.Response(Cancelled: 0, Considered: 1, Failed: 1), first);

        var rerun = await RunSweepAsync();

        Assert.Equal(new AutoCancelStaleRecurringOrders.Response(Cancelled: 1, Considered: 1, Failed: 0), rerun);
        await using var verify = NewContext();
        Assert.Equal(OrderStatus.Cancelled, (await verify.Orders.IgnoreQueryFilters()
            .SingleAsync(o => o.Id == OrderId)).CurrentStatus);
        Assert.Equal(Credit, Assert.Single(await verify.CreditTransactions.IgnoreQueryFilters()
            .Where(t => t.IdempotencyKey == ReturnKey).ToListAsync()).Amount);
        var account = await verify.CreditAccounts.IgnoreQueryFilters().Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == UserId);
        Assert.Equal(Credit, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Single(await verify.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
        Assert.Single(await verify.OutboxMessages.IgnoreQueryFilters().ToListAsync());
    }

    /// <summary>
    /// The customer's payment for the occurrence lands while its cancellation is being staged. The order's payment
    /// status is a concurrency token, so the cancellation's commit is refused and rolls back with its credit return:
    /// the occurrence stays paid and booked with its credit still spent, and the run counts it as failed. Without
    /// the token the cancellation landed on the paid occurrence and handed its credit back.
    /// </summary>
    [Fact]
    public async Task A_Payment_Landing_While_The_Occurrence_Is_Cancelled_Is_Kept_And_Nothing_Is_Cancelled()
    {
        var paidMeanwhile = new BeforeFirstOwnerLock(PayAsync);

        var run = await RunSweepAsync(paidMeanwhile);

        await using (var afterRace = NewContext())
        {
            var order = await afterRace.Orders.IgnoreQueryFilters()
                .Include(o => o.OrderStatusHistory)
                .SingleAsync(o => o.Id == OrderId);
            var creditReturned = await afterRace.CreditTransactions.IgnoreQueryFilters().AnyAsync(t => t.IdempotencyKey == ReturnKey);
            var notices = await afterRace.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
            var intents = await afterRace.OutboxMessages.IgnoreQueryFilters().ToListAsync();
            Assert.Multiple(
                () => Assert.Equal((PaymentStatus.Paid, OrderStatus.New), (order.PaymentStatus, order.CurrentStatus)),
                () => Assert.DoesNotContain(order.OrderStatusHistory, t => t.Status == OrderStatus.Cancelled),
                () => Assert.False(creditReturned),
                () => Assert.Empty(notices),
                () => Assert.Empty(intents));
        }

        Assert.Equal(0, (await RunSweepAsync()).Cancelled);
        Assert.True(paidMeanwhile.Ran);
        Assert.Equal(new AutoCancelStaleRecurringOrders.Response(Cancelled: 0, Considered: 1, Failed: 1), run);
    }

    /// <summary>The webhook's write of the occurrence's card payment, committed on a connection of its own.</summary>
    private async Task PayAsync()
    {
        await using var paying = NewContext();
        var order = await paying.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
        order.UpdatePaymentStatus(PaymentStatus.Paid);
        await paying.CommitAsync(CancellationToken.None);
    }

    /// <summary>
    /// The sweep in a scope of its own and each occurrence's unit in another, each inside the real unit-of-work
    /// pipeline, as the Functions host runs them. Only the mediator is a stand-in.
    /// </summary>
    private async Task<AutoCancelStaleRecurringOrders.Response> RunSweepAsync(params IInterceptor[] interceptors)
    {
        await using var provider = BuildProvider(interceptors);
        await using var sweepScope = provider.CreateAsyncScope();
        var command = new AutoCancelStaleRecurringOrders.Command();
        var handler = new AutoCancelStaleRecurringOrders.Handler(
            sweepScope.ServiceProvider.GetRequiredService<IOrderRepository>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AutoCancelStaleRecurringOrders.Handler>.Instance);

        var result = await InUnitOfWork(sweepScope.ServiceProvider, command, handler.Handle, CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private ServiceProvider BuildProvider(IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantProvider>(_ => new MutableTenantProvider());
        services.AddScoped(sp => NewContext(sp.GetRequiredService<ITenantProvider>(), interceptors));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CleansiaDbContext>());
        services.AddScoped<IOrderRepository>(sp => new OrderRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<ICreditAccountRepository>(
            sp => new CreditAccountRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<INotificationProducer>(sp =>
        {
            var ctx = sp.GetRequiredService<CleansiaDbContext>();
            return new NotificationProducer(
                new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx),
                NullLogger<NotificationProducer>.Instance);
        });
        services.AddScoped<AutoCancelRecurringOccurrence.Handler>();
        services.AddScoped<IMediator>(sp =>
        {
            var mediator = new Mock<IMediator>();
            mediator
                .Setup(m => m.Send(It.IsAny<AutoCancelRecurringOccurrence.Command>(), It.IsAny<CancellationToken>()))
                .Returns((AutoCancelRecurringOccurrence.Command c, CancellationToken ct) => InUnitOfWork(
                    sp, c, sp.GetRequiredService<AutoCancelRecurringOccurrence.Handler>().Handle, ct));
            return mediator.Object;
        });

        return services.BuildServiceProvider();
    }

    private static Task<BusinessResult<TResponse>> InUnitOfWork<TCommand, TResponse>(
        IServiceProvider scope,
        TCommand command,
        Func<TCommand, CancellationToken, Task<BusinessResult<TResponse>>> handle,
        CancellationToken cancellationToken)
        where TCommand : notnull =>
        new UnitOfWorkPipelineBehavior<TCommand, BusinessResult<TResponse>>(scope.GetRequiredService<IUnitOfWork>())
            .Handle(command, ct => handle(command, ct), cancellationToken);

    private CleansiaDbContext NewContext(params IInterceptor[] interceptors) =>
        NewContext(_tenantProvider, interceptors);

    private CleansiaDbContext NewContext(ITenantProvider tenantProvider, params IInterceptor[] interceptors) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).AddInterceptors(interceptors).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            tenantProvider);

    /// <summary>The Orders FKs are dropped so the fixture can name a template without its whole graph.</summary>
    private static async Task DropOrderForeignKeysAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(
            """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN SELECT conname, conrelid::regclass AS tbl FROM pg_constraint
                       WHERE contype = 'f' AND conrelid = '"Orders"'::regclass
              LOOP
                EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname);
              END LOOP;
            END $$;
            """,
            conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>An unconfirmed card occurrence half an hour from its slot, with 300 of credit applied to it.</summary>
    private async Task SeedAsync()
    {
        _tenantProvider.SetTenantOverride(TestTenants.Default);
        await using var ctx = NewContext();

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        ctx.Currencies.Add(currency);
        ctx.Languages.Add(Language.Create("en", "English"));

        var customer = User.CreateWithPassword($"{UserId}@cleansia.test", "Passw0rd!", "Rita", "Recurring");
        customer.Id = UserId;
        ctx.Users.Add(customer);

        var order = Order.Create(
            customerName: "Rita Recurring",
            customerEmail: $"{UserId}@cleansia.test",
            customerPhone: "+420777000111",
            customerAddress: Address.Create("Recurring St 1", "Brno", "60200", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(30),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: UserId,
            recurringTemplateId: "tmpl-recurring-credit",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.ApplyCredit(Credit, "seed");
        ctx.Orders.Add(order);

        await ctx.CommitAsync(CancellationToken.None);
        _tenantProvider.ClearTenantOverride();
    }

    /// <summary>Refuses the first save, as a conflict or a dropped connection at the cancellation's commit would.</summary>
    private sealed class RefuseFirstSave : SaveChangesInterceptor
    {
        private bool _refused;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_refused)
            {
                _refused = true;
                throw new InvalidOperationException("The cancellation's commit was refused.");
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Commits another connection's write just before the first owner lock, the credit return's.</summary>
    private sealed class BeforeFirstOwnerLock(Func<Task> write) : DbCommandInterceptor
    {
        public bool Ran { get; private set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!Ran
                && command.CommandText.Contains("FROM \"Users\"", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR NO KEY UPDATE", StringComparison.Ordinal))
            {
                Ran = true;
                await write();
            }

            return result;
        }
    }

    private sealed class MutableTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
