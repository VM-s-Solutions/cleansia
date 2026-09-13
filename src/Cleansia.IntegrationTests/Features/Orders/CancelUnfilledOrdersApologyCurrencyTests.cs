using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The apology credit is authored PER CURRENCY (<c>Currency.NoShowCredit</c>, ADR-0060 D1) and lands
/// in the customer's credit account in the ORDER's currency, against real Postgres — the currency
/// rides the sweep's <c>Include</c>, the account is keyed per currency by a unique index, and the
/// push is an outbox row. Three orders, three currencies states, one sweep.
/// </summary>
[Collection("PostgresCollection")]
public class CancelUnfilledOrdersApologyCurrencyTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-apology";
    private const string CzkId = "cur-czk-apology";
    private const string EurId = "cur-eur-apology";
    private const string NokId = "cur-nok-apology";

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));
    }

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
    }

    private static Currency NewCurrency(string id, string code, bool isDefault, decimal? noShowCredit)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        currency.IsActive = true;
        currency.SetAsDefault(isDefault);
        currency.SetNoShowCredit(noShowCredit);
        return currency;
    }

    /// <summary>A cash booking two hours past its slot with nobody on it: swept, nothing to refund.</summary>
    private static Order UnfilledCashOrder(string orderId, string currencyId, string userId)
    {
        var order = Order.Create(
            customerName: "Apology Customer",
            customerEmail: "apology@cleansia.test",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(-2),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: userId);
        order.Id = orderId;
        order.SetMaxEmployees(1);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        return order;
    }

    private async Task<string> SeedAsync()
    {
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);

        ctx.Currencies.AddRange(
            NewCurrency(CzkId, "CZK", isDefault: true, noShowCredit: 250m),
            NewCurrency(EurId, "EUR", isDefault: false, noShowCredit: null),
            NewCurrency(NokId, "NOK", isDefault: false, noShowCredit: 10m));

        var user = User.CreateWithPassword("apology@cleansia.test", "Seed-Password-123", "Apology", "Customer");
        ctx.Users.Add(user);

        ctx.Orders.AddRange(
            UnfilledCashOrder("order-czk-apology", CzkId, user.Id),
            UnfilledCashOrder("order-eur-apology", EurId, user.Id),
            UnfilledCashOrder("order-nok-apology", NokId, user.Id));

        await ctx.CommitAsync(CancellationToken.None);
        return user.Id;
    }

    private async Task<CancelUnfilledOrders.Response> SweepAsync()
    {
        await using var ctx = NewContext();
        var handler = new CancelUnfilledOrders.Handler(
            new OrderRepository(ctx),
            new CreditAccountRepository(ctx),
            new NoRefunds(),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)),
            new FixedTenantProvider(tenantId: null),
            ctx,
            NullLogger<CancelUnfilledOrders.Handler>.Instance);

        var result = await handler.Handle(new CancelUnfilledOrders.Command(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    [Fact]
    public async Task Each_Order_Is_Credited_Its_Own_Currencys_Figure_Into_An_Account_In_That_Currency()
    {
        await ResetAsync();
        var userId = await SeedAsync();

        var response = await SweepAsync();

        Assert.Equal(3, response.CancelledCount);
        Assert.Equal(0, response.RefundedCount);
        Assert.Equal(2, response.CreditedCount);

        await using var ctx = NewContext();

        var accounts = await ctx.CreditAccounts
            .IgnoreQueryFilters()
            .Include(a => a.Transactions)
            .Where(a => a.UserId == userId)
            .ToListAsync();

        var czk = Assert.Single(accounts, a => a.CurrencyId == CzkId);
        Assert.Equal(250m, czk.Balance);
        var czkRow = Assert.Single(czk.Transactions);
        Assert.Equal("cleaner-noshow:order-czk-apology", czkRow.IdempotencyKey);

        var nok = Assert.Single(accounts, a => a.CurrencyId == NokId);
        Assert.Equal(10m, nok.Balance);
        Assert.Equal("cleaner-noshow:order-nok-apology", Assert.Single(nok.Transactions).IdempotencyKey);

        // EUR authors no figure: refunded (nothing to refund here), never credited, no account opened.
        Assert.DoesNotContain(accounts, a => a.CurrencyId == EurId);

        var orders = await ctx.Orders.IgnoreQueryFilters().ToListAsync();
        Assert.All(orders, o => Assert.Equal(OrderStatus.Cancelled, o.CurrentStatus));

        var pushes = await ctx.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.NotificationsDispatch)
            .Select(m => m.Body)
            .ToListAsync();
        Assert.Equal(2, pushes.Count(b => b.Contains(NotificationEventCatalog.OrderNoCleanerRefunded)));
        Assert.Equal(1, pushes.Count(b => b.Contains(NotificationEventCatalog.OrderCancelled)));

        // The push and the feed row carry the credit WITH its own currency (owner ruling 2026-09-13,
        // Q-MARKET-04); these currencies are seeded with the code as the symbol.
        Assert.Single(pushes, b => b.Contains("250 CZK"));
        Assert.Single(pushes, b => b.Contains("10 NOK"));
        var feedArgs = await ctx.Set<UserNotification>()
            .IgnoreQueryFilters()
            .Where(n => n.EventKey == NotificationEventCatalog.OrderNoCleanerRefunded)
            .Select(n => n.ArgsJson)
            .ToListAsync();
        Assert.Equal(2, feedArgs.Count);
        Assert.Single(feedArgs, a => a.Contains("250 CZK"));
        Assert.Single(feedArgs, a => a.Contains("10 NOK"));
    }

    [Fact]
    public async Task A_Second_Sweep_Pays_Nothing_Twice()
    {
        await ResetAsync();
        var userId = await SeedAsync();

        await SweepAsync();
        var second = await SweepAsync();

        Assert.Equal(0, second.CancelledCount);
        Assert.Equal(0, second.CreditedCount);

        await using var ctx = NewContext();
        var czk = await ctx.CreditAccounts
            .IgnoreQueryFilters()
            .FirstAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
        Assert.Equal(250m, czk.Balance);
    }

    /// <summary>Every order here is cash, so a refund call would be a bug rather than a Stripe round trip.</summary>
    private sealed class NoRefunds : IRefundService
    {
        public Task<BusinessResult<RefundResult>> IssueRefundAsync(RefundRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"No refund was expected for order {request.OrderId}.");
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
