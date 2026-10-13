using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
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

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// An administrator cancels a card checkout that was never paid, on which 300 of credit was spent. The credit
/// comes back with the commit that cancels the order, or not at all: when that commit is refused the order is
/// still open with its credit still spent, and the retried cancel returns the 300 once.
/// </summary>
[Collection("PostgresCollection")]
public class PlatformCancellationCreditReturnAtomicityTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-cancel-atomic";
    private const string CzkId = "cur-czk-cancel-atomic";
    private const string OrderId = "order-cancel-atomic";
    private const string ActorId = "admin-cancel-atomic";
    private const string ReturnKey = $"credit-return:order-ended-unpaid:{OrderId}";
    private const decimal Credit = 300m;

    private CleansiaDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .AddInterceptors(interceptors)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider(ActorId, "admin@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));
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
        await SeedTenantRegistryAsync(conn);
    }

    /// <summary>
    /// The state an abandoned checkout leaves: a 300 grant spent on the order under its payment key, so the
    /// balance is zero and still equals the ledger, and an unpaid card order that is still open.
    /// </summary>
    private async Task<string> SeedAsync()
    {
        string userId;
        await using (var ctx = NewContext())
        {
            ctx.Languages.Add(Language.Create("en", "English"));
            var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
            country.Id = CountryId;
            ctx.Countries.Add(country);
            var currency = Currency.Create("CZK", "CZK", "CZK");
            currency.Id = CzkId;
            currency.IsActive = true;
            currency.SetAsDefault(true);
            ctx.Currencies.Add(currency);
            var user = User.CreateWithPassword("cancel-atomic@cleansia.test", "Seed-Password-123", "Cancel", "Atomic");
            ctx.Users.Add(user);
            await ctx.CommitAsync(CancellationToken.None);
            userId = user.Id;
        }

        await using (var ctx = NewContext())
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserIgnoringTenantAsync(userId, CzkId, CancellationToken.None);
            account!.Issue(Credit, CreditTransactionReason.Goodwill, "seed-grant", "seed", note: "n");

            var order = Order.Create(
                customerName: "Cancel Atomic",
                customerEmail: "cancel-atomic@cleansia.test",
                customerPhone: "+420000000000",
                customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddDays(3),
                paymentType: PaymentType.Card,
                totalPrice: 1000m,
                currencyId: CzkId,
                paymentStatus: PaymentStatus.Pending,
                userId: userId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            order.SetMaxEmployees(1);
            order.ApplyCredit(Credit, userId);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
            ctx.Orders.Add(order);
            await ctx.CommitAsync(CancellationToken.None);

            Assert.True(await new CreditAccountRepository(ctx).TryDebitAsync(
                account.Id, Credit, CreditTransactionReason.OrderPayment, $"order-payment-{OrderId}", userId,
                CancellationToken.None, orderId: OrderId));
        }

        return userId;
    }

    private static async Task CancelAndCommitAsync(CleansiaDbContext ctx)
    {
        var order = await new OrderRepository(ctx).GetByIdAsync(OrderId, CancellationToken.None);
        await new PlatformOrderCancellation(
                Mock.Of<IRefundService>(),
                new RefundRepository(ctx),
                new CreditAccountRepository(ctx),
                Mock.Of<ILoyaltyService>(),
                Mock.Of<INotificationProducer>(),
                Mock.Of<ILiveActivityProducer>(),
                Mock.Of<IExpressWaiverConsumer>(),
                new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
                Mock.Of<IPendingDispatch>(),
                NullLogger<PlatformOrderCancellation>.Instance)
            .CancelAsync(order!, ActorId, CancelledBy.Admin, "Double booking on our side",
                RefundReason.CustomerCancellation, CancellationToken.None);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<(Order Order, CreditAccount Account)> ReadAsync(string userId)
    {
        await using var ctx = NewContext();
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking()
            .Include(o => o.OrderStatusHistory)
            .SingleAsync(o => o.Id == OrderId);
        var account = await ctx.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
        return (order, account);
    }

    [Fact]
    public async Task A_Refused_Cancel_Returns_No_Credit_And_Its_Retry_Returns_It_Once()
    {
        await ResetAsync();
        var userId = await SeedAsync();

        await using (var refused = NewContext(new RefuseFirstSave()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CancelAndCommitAsync(refused));
        }

        var (order, account) = await ReadAsync(userId);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.DoesNotContain(account.Transactions, t => t.IdempotencyKey == ReturnKey);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));

        await using (var retry = NewContext())
        {
            await CancelAndCommitAsync(retry);
        }

        (order, account) = await ReadAsync(userId);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(Credit, Assert.Single(account.Transactions, t => t.IdempotencyKey == ReturnKey).Amount);
        Assert.Equal(Credit, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
    }

    /// <summary>Refuses the first save, as a conflict or a dropped connection at the cancel's commit would.</summary>
    private sealed class RefuseFirstSave : SaveChangesInterceptor
    {
        private bool _refused;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_refused)
            {
                _refused = true;
                throw new InvalidOperationException("The cancel's commit was refused.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
