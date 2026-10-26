using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MediatR;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Config;
using System.Security.Claims;
using System.Data.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;
using Xunit.Abstractions;

namespace Cleansia.IntegrationTests.Features.Orders;

[Collection("PostgresCollection")]
public sealed class CancelUnfilledOrdersFreshReadTests(PostgresContainerFixture fixture, ITestOutputHelper output) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "order-unfilled-fresh";
    private const string CleanerId = "cleaner-unfilled-fresh";
    private const string CountryId = "country-unfilled-fresh";
    private const string CurrencyId = "currency-unfilled-fresh";
    private readonly Mock<IStripeClient> _stripe = new();

    private CleansiaDbContext NewContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).AddInterceptors(interceptors).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"), new FixedTenantProvider(TestTenants.Default));

    private async Task SeedAsync(PaymentType paymentType = PaymentType.Card)
    {
        await using var connection = new NpgsqlConnection(Fixture.GetConnectionString());
        await connection.OpenAsync();
        var reset = await Respawner.CreateAsync(connection, new RespawnerOptions
        { DbAdapter = DbAdapter.Postgres, SchemasToExclude = ["pg_catalog", "information_schema"] });
        await reset.ResetAsync(connection);
        await SeedTenantRegistryAsync(connection);
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);
        ctx.CountryConfigurations.Add(CountryConfiguration.Create(CountryId, "CZK", "en", 0.21m));
        var (contract, _) = TestLegalDocuments.Add(ctx);
        var currency = Currency.Create("CZK", "CZK", "CZK");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        currency.SetNoShowCredit(250m);
        ctx.Currencies.Add(currency);
        var customer = User.CreateWithPassword("fresh-customer@cleansia.test", "Password123!", "Rita", "Customer");
        ctx.Users.Add(customer);
        var cleanerUser = User.CreateWithPassword("fresh-cleaner@cleansia.test", "Password123!", "Rita", "Cleaner", UserProfile.Employee);
        cleanerUser.Id = "usr-unfilled-cleaner";
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = CleanerId;
        cleaner.UpdateContractStatus(ContractStatus.Approved);
        cleaner.AssignWorkCountry(CountryId);
        cleaner.UpdateAddress(Address.Create("Cleaner St 9", "Brno", "60200", CountryId));
        ctx.Employees.Add(cleaner);
        var order = Order.Create("Rita Customer", customer.Email, "+420000000000",
            Address.Create("Test St 1", "Brno", "60200", CountryId), 1, 1, DateTime.UtcNow.AddMinutes(-31),
            paymentType, 1000m, CurrencyId, paymentType == PaymentType.Card ? PaymentStatus.Paid : PaymentStatus.Pending,
            userId: customer.Id, cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.UpdateEstimatedTime(120);
        order.SetWorkContractDocument(contract);
        order.SetMaxEmployees(1);
        order.AssignStripePaymentIntentId("pi_unfilled_fresh");
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("take")]
    [InlineData("mediator_take")]
    [InlineData("on_the_way")]
    [InlineData("in_progress")]
    [InlineData("missing")]
    [InlineData("moved_operator")]
    public async Task A_Current_Crew_Status_Or_Operator_Change_Is_Seen_Before_NoShow_Claim(string change)
    {
        await SeedAsync();
        var priorFeed = 0;
        var priorOutbox = 0;
        var race = new AfterCandidatesRead(async () =>
        {
            await using var other = NewContext();
            var order = await other.Orders.Include(o => o.AssignedEmployees).Include(o => o.OrderStatusHistory).SingleAsync(o => o.Id == OrderId);
            if (change == "mediator_take")
            {
                await TakeWithMediatorAsync();
                priorFeed = await other.Set<UserNotification>().IgnoreQueryFilters().CountAsync();
                priorOutbox = await other.OutboxMessages.IgnoreQueryFilters().CountAsync();
            }
            else if (change == "take")
            {
                Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
                Assert.True(order.HasAvailableSpots);
                order.AddAssignedEmployee(OrderEmployee.Create(order, await other.Employees.SingleAsync(e => e.Id == CleanerId)));
                await other.CommitAsync(CancellationToken.None);
                Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
            }
            else if (change == "missing")
                await other.Orders.Where(o => o.Id == OrderId).ExecuteDeleteAsync();
            else if (change == "moved_operator")
                await other.Orders.Where(o => o.Id == OrderId).ExecuteUpdateAsync(s => s.SetProperty(o => o.TenantId, TestTenants.Second));
            else
            {
                order.AddOrderStatus(OrderStatusTrack.Create(change == "on_the_way" ? OrderStatus.OnTheWay : OrderStatus.InProgress, order));
                await other.CommitAsync(CancellationToken.None);
            }
        });

        var result = await SweepAsync(race);

        Assert.True(race.Ran);
        await using var read = NewContext();
        var persisted = await read.Orders.IgnoreQueryFilters().AsNoTracking().Include(o => o.AssignedEmployees).SingleOrDefaultAsync(o => o.Id == OrderId);
        output.WriteLine($"change={change}; cancelled={result.CancelledCount}; persisted={persisted?.CurrentStatus}; crew={persisted?.AssignedEmployees.Count}; refund rows={await read.Refunds.IgnoreQueryFilters().CountAsync()}; ledger={await read.CreditTransactions.IgnoreQueryFilters().CountAsync()}; feed={await read.Set<UserNotification>().IgnoreQueryFilters().CountAsync()}; outbox={await read.OutboxMessages.IgnoreQueryFilters().CountAsync()}");
        Assert.Equal(0, result.CancelledCount);
        if (change is "take" or "mediator_take")
        {
            Assert.Equal(OrderStatus.Confirmed, persisted!.CurrentStatus);
            Assert.Single(persisted.AssignedEmployees);
        }
        Assert.Empty(await read.Set<OrderStatusTrack>().IgnoreQueryFilters().Where(t => t.OrderId == OrderId && t.Status == OrderStatus.Cancelled).ToListAsync());
        Assert.Empty(await read.Refunds.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await read.CreditTransactions.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(priorFeed, await read.Set<UserNotification>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(priorOutbox, await read.OutboxMessages.IgnoreQueryFilters().CountAsync());
        _stripe.Verify(s => s.RefundPaymentIntentAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(PaymentType.Card)]
    [InlineData(PaymentType.Cash)]
    public async Task An_Unchanged_Qualifying_Order_Is_Cancelled_Once(PaymentType paymentType)
    {
        await SeedAsync(paymentType);
        Assert.Equal(1, (await SweepAsync()).CancelledCount);
        Assert.Equal(0, (await SweepAsync()).CancelledCount);
        await using var read = NewContext();
        Assert.Equal(OrderStatus.Cancelled, (await read.Orders.SingleAsync()).CurrentStatus);
        Assert.Single(await read.Set<OrderStatusTrack>().Where(t => t.Status == OrderStatus.Cancelled).ToListAsync());
        Assert.Equal(paymentType == PaymentType.Card ? 1 : 0, await read.Refunds.CountAsync());
        Assert.Single(await read.CreditTransactions.ToListAsync());
        Assert.Single(await read.Set<UserNotification>().ToListAsync());
        var outbox = await read.OutboxMessages.Select(m => m.MessageKey).ToListAsync();
        Assert.Equal(2, outbox.Count);
        Assert.Single(outbox, key => key.StartsWith("push:", StringComparison.Ordinal));
        Assert.Single(outbox, key => key == MessageKeys.GuestOrderCancelledEmail(OrderId));
    }

    private async Task TakeWithMediatorAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IConfiguration>(Configuration);
        services.AddCoreBindings(Configuration, new TestHostEnvironment());
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(_ => new FixedTenantProvider(TestTenants.Default)));
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            "usr-unfilled-cleaner", "fresh-cleaner@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
             new Claim(TestUserSessionProvider.EmployeeIdClaimType, CleanerId)])));
        services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider(JwtAudiences.Partner));
        services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => Mock.Of<IEmailService>()));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new TakeOrder.Command(OrderId, TestLegalDocuments.WorkContractTextEnId));
        Assert.True(result.IsSuccess, result.Error?.Message);
        await using var read = NewContext();
        Assert.Equal(OrderStatus.Confirmed, (await read.Orders.SingleAsync(o => o.Id == OrderId)).CurrentStatus);
        Assert.Single(await read.OrderEmployees.Where(e => e.OrderId == OrderId).ToListAsync());
        Assert.Single(await read.WorkContractAcceptances.Where(a => a.OrderId == OrderId).ToListAsync());
        output.WriteLine("actual mediator TakeOrder success; validator + UoW pipeline; Confirmed unchanged; one seat/acceptance committed");
    }

    private async Task<CancelUnfilledOrders.Response> SweepAsync(params IInterceptor[] interceptors)
    {
        await using var ctx = NewContext(interceptors);
        var pending = new OutboxPendingDispatch(ctx);
        var refunds = new RefundRepository(ctx);
        var credit = new CreditAccountRepository(ctx);
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        var handler = new CancelUnfilledOrders.Handler(new OrderRepository(ctx),
            new CleanerNoShowCancellation(credit,
                new RefundService(refunds, new OrderRepository(ctx), credit, factory.Object, Mock.Of<IAdminNotifier>(),
                    new UserNotificationRepository(ctx), NullLogger<RefundService>.Instance),
                refunds, new NotificationProducer(new UserNotificationRepository(ctx), pending, new UserRepository(ctx), NullLogger<NotificationProducer>.Instance),
                new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)), pending, NullLogger<CleanerNoShowCancellation>.Instance),
            new FixedTenantProvider(TestTenants.Default), ctx, NullLogger<CancelUnfilledOrders.Handler>.Instance);
        var result = await handler.Handle(new CancelUnfilledOrders.Command(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private sealed class AfterCandidatesRead(Func<Task> change) : DbCommandInterceptor
    {
        public bool Ran { get; private set; }
        public override async ValueTask<InterceptionResult> DataReaderClosingAsync(DbCommand command,
            DataReaderClosingEventData data, InterceptionResult result)
        {
            if (!Ran && command.CommandText.Contains("CleaningDateTime", StringComparison.Ordinal)
                && data.Context!.ChangeTracker.Entries<Order>().Any(e => e.Entity.Id == OrderId))
            {
                Ran = true;
                Assert.Empty(data.Context.ChangeTracker.Entries<Order>().Single(e => e.Entity.Id == OrderId).Entity.AssignedEmployees);
                await change();
            }
            return result;
        }
    }

    private sealed class FixedTenantProvider(string tenantId) : ITenantProvider
    {
        private string? _tenant = tenantId;
        public string? GetCurrentTenantId() => _tenant;
        public void SetTenantOverride(string tenant) => _tenant = tenant;
        public void ClearTenantOverride() => _tenant = null;
    }
}
