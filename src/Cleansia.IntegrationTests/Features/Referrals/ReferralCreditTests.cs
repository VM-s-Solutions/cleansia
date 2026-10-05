using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Referrals;

/// <summary>
/// The referral reward is credit (owner ruling 2026-10-04), against real Postgres: the grant, the admin
/// force-qualify and the admin reversal all move money through <c>CreditAccounts</c>, and the reversal's
/// debit is the conditional UPDATE no in-memory provider runs. Every test also holds the ledger
/// invariant <c>Balance == SUM(Transactions.Amount)</c>.
/// </summary>
[Collection("PostgresCollection")]
public class ReferralCreditTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "01ADMINREFERRAL00000000001";
    private const string CountryId = "country-cze-referral";
    private const string CzkId = "currency-czk-referral";
    private const string EurId = "currency-eur-referral";
    private const string OrderId = "order-referral-first";
    private const string Reason = "referral ring";

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
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

    private static Currency NewCurrency(string id, string code, bool isDefault, decimal referralCredit)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        currency.IsActive = true;
        currency.SetAsDefault(isDefault);
        currency.SetReferralCredit(referralCredit);
        return currency;
    }

    /// <summary>
    /// Inviter, invited friend and the accepted referral between them; with <paramref name="orderCurrencyId"/>,
    /// also the friend's first order in that currency, completed.
    /// </summary>
    private async Task<(string ReferrerId, string ReferredId, string ReferralId)> SeedAsync(
        string? orderCurrencyId, bool eraseReferrer = false)
    {
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);
        ctx.Currencies.AddRange(
            NewCurrency(CzkId, "CZK", isDefault: true, referralCredit: 150m),
            NewCurrency(EurId, "EUR", isDefault: false, referralCredit: 6m));

        var referrer = User.CreateWithPassword("referrer@cleansia.test", "Seed-Password-123", "Referring", "Friend");
        var referred = User.CreateWithPassword("referred@cleansia.test", "Seed-Password-123", "Invited", "Friend");
        ctx.Users.AddRange(referrer, referred);

        var code = ReferralCode.Generate(referrer.Id, "RFCRDT", "system");
        ctx.ReferralCodes.Add(code);
        var referral = Referral.CreateAccepted(referrer.Id, referred.Id, code.Id, "system");
        ctx.Referrals.Add(referral);

        if (orderCurrencyId is not null)
        {
            var order = Order.Create(
                customerName: "Invited Friend",
                customerEmail: "referred@cleansia.test",
                customerPhone: "+420000000000",
                customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddHours(-3),
                paymentType: PaymentType.Cash,
                totalPrice: 1000m,
                currencyId: orderCurrencyId,
                paymentStatus: PaymentStatus.Paid,
                userId: referred.Id,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            order.SetMaxEmployees(1);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
            ctx.Orders.Add(order);
        }

        if (eraseReferrer)
        {
            referrer.Anonymize();
            referrer.IsActive = false;
        }

        await ctx.CommitAsync(CancellationToken.None);
        return (referrer.Id, referred.Id, referral.Id);
    }

    private static ReferralService Service(CleansiaDbContext ctx) => new(
        new ReferralCodeRepository(ctx),
        new ReferralRepository(ctx),
        new OrderRepository(ctx),
        new CreditAccountRepository(ctx),
        ctx,
        NullLogger<ReferralService>.Instance);

    private async Task CompleteAsync(string referredId)
    {
        await using var ctx = NewContext();
        await Service(ctx).ProcessOrderCompletedAsync(OrderId, referredId, CancellationToken.None);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<ForceQualifyReferral.Response> ForceQualifyAsync(string referralId)
    {
        await using var ctx = NewContext();
        var result = await new ForceQualifyReferral.Handler(
                new ReferralRepository(ctx),
                Service(ctx),
                new OrderRepository(ctx),
                new CurrencyRepository(ctx),
                new TestUserSessionProvider(AdminId, "admin@cleansia.test"))
            .Handle(new ForceQualifyReferral.Command(referralId, Reason), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await ctx.CommitAsync(CancellationToken.None);
        return result.Value!;
    }

    private async Task<ReverseReferral.Response> ReverseAsync(string referralId)
    {
        await using var ctx = NewContext();
        var result = await new ReverseReferral.Handler(
                new ReferralRepository(ctx),
                new CreditAccountRepository(ctx),
                new CurrencyRepository(ctx),
                new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
                NullLogger<ReverseReferral.Handler>.Instance)
            .Handle(new ReverseReferral.Command(referralId, Reason), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await ctx.CommitAsync(CancellationToken.None);
        return result.Value!;
    }

    private async Task<List<CreditAccount>> AccountsAsync(string userId)
    {
        await using var ctx = NewContext();
        var accounts = await ctx.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Transactions)
            .Where(a => a.UserId == userId)
            .ToListAsync();
        Assert.All(accounts, a => Assert.Equal(a.Balance, a.Transactions.Sum(t => t.Amount)));
        return accounts;
    }

    private async Task<Referral> ReferralAsync(string referralId)
    {
        await using var ctx = NewContext();
        return await ctx.Referrals.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == referralId);
    }

    [Fact]
    public async Task A_Completed_First_Order_Credits_Both_Sides_Once_In_Its_Currency()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: EurId);

        await CompleteAsync(referredId);
        await CompleteAsync(referredId);

        foreach (var (userId, side) in new[] { (referrerId, "referrer"), (referredId, "referred") })
        {
            var account = Assert.Single(await AccountsAsync(userId));
            Assert.Equal(EurId, account.CurrencyId);
            Assert.Equal(TestTenants.Default, account.TenantId);
            Assert.Equal(6m, account.Balance);
            var grant = Assert.Single(account.Transactions);
            Assert.Equal(CreditTransactionReason.Referral, grant.Reason);
            Assert.Equal($"referral:{referralId}:{side}", grant.IdempotencyKey);
            Assert.Equal(OrderId, grant.OrderId);
        }

        var referral = await ReferralAsync(referralId);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(EurId, referral.CreditCurrencyId);
        Assert.Equal(6m, referral.CreditAwardedToReferrer);
        Assert.Equal(6m, referral.CreditAwardedToReferred);
    }

    [Fact]
    public async Task An_Erased_Inviter_Receives_Nothing_And_The_Friend_Is_Still_Paid()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: CzkId, eraseReferrer: true);

        await CompleteAsync(referredId);

        Assert.Empty(await AccountsAsync(referrerId));
        Assert.Equal(150m, Assert.Single(await AccountsAsync(referredId)).Balance);
        var referral = await ReferralAsync(referralId);
        Assert.Null(referral.CreditAwardedToReferrer);
        Assert.Equal(150m, referral.CreditAwardedToReferred);
    }

    [Fact]
    public async Task Force_Qualify_Credits_Both_Sides_In_The_Currency_Of_The_Friends_Latest_Order()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: EurId);

        var response = await ForceQualifyAsync(referralId);

        Assert.Equal(new ForceQualifyReferral.Response(referralId, 6m, 6m, "EUR"), response);
        Assert.Equal(6m, Assert.Single(await AccountsAsync(referrerId)).Balance);
        Assert.Equal(6m, Assert.Single(await AccountsAsync(referredId)).Balance);
        Assert.Equal(ReferralStatus.Qualified, (await ReferralAsync(referralId)).Status);
    }

    [Fact]
    public async Task Force_Qualify_For_A_Friend_Who_Never_Booked_Pays_In_The_Platform_Default()
    {
        await ResetAsync();
        var (referrerId, _, referralId) = await SeedAsync(orderCurrencyId: null);

        var response = await ForceQualifyAsync(referralId);

        Assert.Equal(new ForceQualifyReferral.Response(referralId, 150m, 150m, "CZK"), response);
        Assert.Equal(CzkId, Assert.Single(await AccountsAsync(referrerId)).CurrencyId);
    }

    [Fact]
    public async Task Reverse_Takes_Back_Both_Grants_In_Full()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: CzkId);
        await CompleteAsync(referredId);

        var response = await ReverseAsync(referralId);

        Assert.Equal(new ReverseReferral.Response(referralId, 150m, 150m, "CZK"), response);
        foreach (var (userId, side) in new[] { (referrerId, "referrer"), (referredId, "referred") })
        {
            var account = Assert.Single(await AccountsAsync(userId));
            Assert.Equal(0m, account.Balance);
            var reversal = Assert.Single(account.Transactions, t => t.Reason == CreditTransactionReason.ReferralReversed);
            Assert.Equal(-150m, reversal.Amount);
            Assert.Equal($"referral-reverse:{referralId}:{side}", reversal.IdempotencyKey);
        }

        Assert.Equal(ReferralStatus.Reversed, (await ReferralAsync(referralId)).Status);
    }

    [Fact]
    public async Task Reverse_On_A_Spent_Balance_Takes_Only_What_Is_Left_And_Never_Goes_Negative()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: CzkId);
        await CompleteAsync(referredId);
        await using (var ctx = NewContext())
        {
            var account = Assert.Single(await AccountsAsync(referredId));
            Assert.True(await new CreditAccountRepository(ctx).TryDebitAsync(
                account.Id, 100m, CreditTransactionReason.OrderPayment, "spent-on-a-booking", referredId,
                CancellationToken.None));
        }

        var response = await ReverseAsync(referralId);

        Assert.Equal(new ReverseReferral.Response(referralId, 150m, 50m, "CZK"), response);
        var spent = Assert.Single(await AccountsAsync(referredId));
        Assert.Equal(0m, spent.Balance);
        Assert.Equal(-50m, Assert.Single(spent.Transactions, t => t.Reason == CreditTransactionReason.ReferralReversed).Amount);
        Assert.Equal(0m, Assert.Single(await AccountsAsync(referrerId)).Balance);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
