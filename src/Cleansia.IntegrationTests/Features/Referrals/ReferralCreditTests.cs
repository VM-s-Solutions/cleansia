using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Referrals;

/// <summary>
/// The referral reward is credit (owner rulings 2026-10-04 and 2026-10-05), against real Postgres: the grant,
/// the admin force-qualify and the admin reversal all move money through <c>CreditAccounts</c>, each side in
/// the currency it books in, and the reversal's debit is the conditional UPDATE no in-memory provider runs. A
/// referral whose two accounts look like one person is held instead, across companies, and the referral row's
/// version makes a release and a rejection of one held row exclusive. Every test also holds the ledger
/// invariant <c>Balance == SUM(Transactions.Amount)</c>.
/// </summary>
[Collection("PostgresCollection")]
public class ReferralCreditTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "01ADMINREFERRAL00000000001";
    private const string CountryId = "country-cze-referral";
    private const string CzkId = "currency-czk-referral";
    private const string EurId = "currency-eur-referral";
    private const string PlnId = "currency-pln-referral";
    private const string OrderId = "order-referral-first";
    private const string ReferrerOrderId = "order-referral-inviter";
    private const string EarlierOrderId = "order-referral-earlier";
    private const string Reason = "referral ring";
    private static readonly DateTimeOffset FrozenOn = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

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

    private static Currency NewCurrency(string id, string code, bool isDefault, decimal? referralCredit)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        currency.IsActive = true;
        currency.SetAsDefault(isDefault);
        currency.SetReferralCredit(referralCredit);
        return currency;
    }

    private static Order NewOrder(
        string id, string userId, string company, string currencyId, Address address, string phone, bool completed)
    {
        var order = Order.Create(
            customerName: "A Customer",
            customerEmail: $"{userId}@cleansia.test",
            customerPhone: phone,
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(-3),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = id;
        order.TenantId = company;
        order.SetMaxEmployees(1);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (completed)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        }

        return order;
    }

    private static Address NewAddress(string street, string zip, string company)
    {
        var address = Address.Create(street, "Prague", zip, CountryId);
        address.TenantId = company;
        return address;
    }

    /// <summary>
    /// Inviter, invited friend and the accepted referral between them, each customer on their company and
    /// the referral on the inviter's; with <paramref name="orderCurrencyId"/>, also the friend's first order
    /// in that currency, on the friend's company, completed unless <paramref name="completed"/> is false; with
    /// <paramref name="referrerOrderCurrencyId"/>, an earlier order of the inviter's own on their company.
    /// </summary>
    private async Task<(string ReferrerId, string ReferredId, string ReferralId)> SeedAsync(
        string? orderCurrencyId,
        bool eraseReferrer = false,
        string referrerCompany = TestTenants.Default,
        string referredCompany = TestTenants.Default,
        bool completed = true,
        string? referrerOrderCurrencyId = null,
        string referrerStreet = "9 Other St",
        string referrerZip = "11000",
        string friendStreet = "123 Main St",
        string friendZip = "11000")
    {
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);
        ctx.Currencies.AddRange(
            NewCurrency(CzkId, "CZK", isDefault: true, referralCredit: 150m),
            NewCurrency(EurId, "EUR", isDefault: false, referralCredit: 6m),
            NewCurrency(PlnId, "PLN", isDefault: false, referralCredit: null));

        var referrer = User.CreateWithPassword("referrer@cleansia.test", "Seed-Password-123", "Referring", "Friend");
        referrer.TenantId = referrerCompany;
        var referred = User.CreateWithPassword("referred@cleansia.test", "Seed-Password-123", "Invited", "Friend");
        referred.TenantId = referredCompany;
        ctx.Users.AddRange(referrer, referred);

        var code = ReferralCode.Generate(referrer.Id, "RFCRDT", "system");
        code.TenantId = referrerCompany;
        ctx.ReferralCodes.Add(code);
        var referral = Referral.CreateAccepted(referrer.Id, referred.Id, code.Id, "system");
        referral.TenantId = referrerCompany;
        ctx.Referrals.Add(referral);

        if (referrerOrderCurrencyId is not null)
        {
            var own = NewOrder(
                ReferrerOrderId, referrer.Id, referrerCompany, referrerOrderCurrencyId,
                NewAddress(referrerStreet, referrerZip, referrerCompany), "+420 601 111 222", completed: false);
            own.Created("customer", DateTime.UtcNow.AddDays(-20));
            ctx.Orders.Add(own);
        }

        if (orderCurrencyId is not null)
        {
            ctx.Orders.Add(NewOrder(
                OrderId, referred.Id, referredCompany, orderCurrencyId,
                NewAddress(friendStreet, friendZip, referredCompany), "+420 000 000 000", completed));
        }

        if (eraseReferrer)
        {
            referrer.Anonymize();
            referrer.IsActive = false;
        }

        await ctx.CommitAsync(CancellationToken.None);
        return (referrer.Id, referred.Id, referral.Id);
    }

    private static AdminNotifier Notifier(CleansiaDbContext ctx) => new(
        new UserRepository(ctx),
        new UserNotificationRepository(ctx),
        new AppConfigurationProvider(ctx),
        new OutboxPendingDispatch(ctx),
        NullLogger<AdminNotifier>.Instance);

    private static ReferralService Service(CleansiaDbContext ctx) => new(
        new ReferralCodeRepository(ctx),
        new ReferralRepository(ctx),
        new OrderRepository(ctx),
        new ReceivableRepository(ctx),
        new CreditAccountRepository(ctx),
        Notifier(ctx),
        ctx,
        NullLogger<ReferralService>.Instance);

    private async Task CompleteAsync(string referredId)
    {
        await using var ctx = NewContext();
        await Service(ctx).ProcessOrderCompletedAsync(OrderId, referredId, CancellationToken.None);
        await ctx.CommitAsync(CancellationToken.None);
    }

    /// <summary>
    /// Freezes the company for archive (ADR-0064 D3) the way its administrators reach it: wound down,
    /// deactivated, then the archive requested.
    /// </summary>
    private Task FreezeAsync(string companyId) => DeactivateAsync(companyId, requestArchive: true);

    private async Task DeactivateAsync(string companyId, bool requestArchive)
    {
        await using var ctx = NewContext();
        var company = await ctx.Tenants.SingleAsync(t => t.Id == companyId);
        company.RequestWindDown(new DateOnly(2026, 9, 1), AdminId, FrozenOn.AddDays(-30));
        company.Deactivate(AdminId, FrozenOn.AddDays(-15));
        if (requestArchive)
        {
            company.RequestArchive(AdminId, FrozenOn);
        }

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static ForceQualifyReferral.Handler ForceQualifyHandler(CleansiaDbContext ctx) => new(
        new ReferralRepository(ctx),
        Service(ctx),
        new OrderRepository(ctx),
        new CurrencyRepository(ctx),
        new TestUserSessionProvider(AdminId, "admin@cleansia.test"));

    private static ReverseReferral.Handler ReverseHandler(CleansiaDbContext ctx) => new(
        new ReferralRepository(ctx),
        new CreditAccountRepository(ctx),
        new CurrencyRepository(ctx),
        new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
        NullLogger<ReverseReferral.Handler>.Instance);

    private async Task<ForceQualifyReferral.Response> ForceQualifyAsync(string referralId, bool expectHeld = false)
    {
        await using var ctx = NewContext();
        var result = await ForceQualifyHandler(ctx)
            .Handle(new ForceQualifyReferral.Command(referralId, Reason, expectHeld), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        await ctx.CommitAsync(CancellationToken.None);
        return result.Value!;
    }

    private async Task<ReverseReferral.Response> ReverseAsync(string referralId, bool expectHeld = false)
    {
        await using var ctx = NewContext();
        var result = await ReverseHandler(ctx)
            .Handle(new ReverseReferral.Command(referralId, Reason, expectHeld), CancellationToken.None);
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

    private async Task SeedSupportAsync(string company)
    {
        await using var ctx = NewContext();
        var support = User.CreateWithPassword(
            "support@cleansia.test", "Seed-Password-123", "Sup", "Port", UserProfile.Administrator, adminRole: AdminRole.Support);
        support.TenantId = company;
        support.ConfirmEmail();
        ctx.Users.Add(support);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<int> ReferralHeldNoticesAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters()
            .CountAsync(n => n.EventKey == AdminNotificationEventCatalog.ReferralHeld);
    }

    private async Task HoldAsync(string referralId)
    {
        await using var ctx = NewContext();
        var referral = await ctx.Referrals.IgnoreQueryFilters().SingleAsync(r => r.Id == referralId);
        referral.HoldForReview(OrderId, Referral.HoldReasonAddress, "system");
        await ctx.CommitAsync(CancellationToken.None);
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
        Assert.Equal(EurId, referral.ReferrerCreditCurrencyId);
        Assert.Equal(EurId, referral.ReferredCreditCurrencyId);
        Assert.Equal(6m, referral.CreditAwardedToReferrer);
        Assert.Equal(6m, referral.CreditAwardedToReferred);
    }

    /// <summary>
    /// The friend's earlier booking, with another company, was not paid to the cleaner at the door and the debt
    /// has been paid since: it is not their first completion, so this one qualifies the referral. The same
    /// earlier booking paid in the ordinary way is, and the referral keeps waiting.
    /// </summary>
    [Theory]
    [InlineData(true, ReferralStatus.Qualified)]
    [InlineData(false, ReferralStatus.Accepted)]
    public async Task A_Booking_Not_Paid_At_The_Door_With_Another_Company_Is_Not_The_Friends_First_Completion(
        bool notPaidAtTheDoor, ReferralStatus expected)
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: CzkId);
        await using (var ctx = NewContext())
        {
            var earlier = NewOrder(
                EarlierOrderId, referredId, TestTenants.Second, CzkId,
                NewAddress("7 Door St", "11000", TestTenants.Second), "+420 000 000 000", completed: true);
            ctx.Orders.Add(earlier);
            if (notPaidAtTheDoor)
            {
                var debt = Receivable.ForUnpaidCash(earlier);
                debt.TenantId = TestTenants.Second;
                debt.MarkPaid(stripePaymentIntentId: null, DateTimeOffset.UtcNow);
                ctx.Receivables.Add(debt);
            }

            await ctx.CommitAsync(CancellationToken.None);
        }

        await CompleteAsync(referredId);

        var referral = await ReferralAsync(referralId);
        Assert.Equal(expected, referral.Status);
        Assert.Equal(notPaidAtTheDoor ? OrderId : null, referral.FirstQualifyingOrderId);
        foreach (var userId in new[] { referrerId, referredId })
        {
            var accounts = await AccountsAsync(userId);
            if (notPaidAtTheDoor)
            {
                var grant = Assert.Single(Assert.Single(accounts).Transactions);
                Assert.Equal(150m, grant.Amount);
                Assert.Equal(OrderId, grant.OrderId);
            }
            else
            {
                Assert.Empty(accounts);
            }
        }
    }

    /// <summary>
    /// The inviter books in koruna with another company; the friend's first booking is in euros. Each side's
    /// balance opens in its own currency, the inviter's on their own company, and the inviter's booking is
    /// found although it is not the ambient company's.
    /// </summary>
    [Fact]
    public async Task A_Cross_Currency_Referral_Pays_Each_Side_In_Its_Own_Currency_Across_Companies()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: EurId, referrerCompany: TestTenants.Second, referrerOrderCurrencyId: CzkId);

        await CompleteAsync(referredId);

        var inviter = Assert.Single(await AccountsAsync(referrerId));
        Assert.Equal(CzkId, inviter.CurrencyId);
        Assert.Equal(TestTenants.Second, inviter.TenantId);
        Assert.Equal(150m, inviter.Balance);
        var friend = Assert.Single(await AccountsAsync(referredId));
        Assert.Equal(EurId, friend.CurrencyId);
        Assert.Equal(6m, friend.Balance);
        var referral = await ReferralAsync(referralId);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(CzkId, referral.ReferrerCreditCurrencyId);
        Assert.Equal(EurId, referral.ReferredCreditCurrencyId);
        Assert.Equal(150m, referral.CreditAwardedToReferrer);
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
        Assert.Null(referral.ReferrerCreditCurrencyId);
        Assert.Equal(150m, referral.CreditAwardedToReferred);
    }

    /// <summary>
    /// The inviter's company is frozen for archive and the friend's first order belongs to another, active
    /// company. The completion of that order commits - the order's own status with it - and pays the friend;
    /// the inviter, whose account would open on the frozen company's books, receives nothing, and the
    /// referral qualifies with what was paid.
    /// </summary>
    [Fact]
    public async Task A_Frozen_Inviters_Company_Does_Not_Stop_The_Friends_Company_Completing_And_Paying_The_Friend()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: CzkId, referrerCompany: TestTenants.Second, completed: false);
        await FreezeAsync(TestTenants.Second);

        await using (var ctx = NewContext())
        {
            var order = await ctx.Orders.Include(o => o.OrderStatusHistory).SingleAsync(o => o.Id == OrderId);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
            await Service(ctx).ProcessOrderCompletedAsync(OrderId, referredId, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking()
                .Include(o => o.OrderStatusHistory).SingleAsync(o => o.Id == OrderId);
            Assert.Contains(order.OrderStatusHistory, h => h.Status == OrderStatus.Completed);
        }

        Assert.Empty(await AccountsAsync(referrerId));
        var paid = Assert.Single(await AccountsAsync(referredId));
        Assert.Equal(TestTenants.Default, paid.TenantId);
        Assert.Equal(150m, paid.Balance);
        var referral = await ReferralAsync(referralId);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Null(referral.CreditAwardedToReferrer);
        Assert.Equal(150m, referral.CreditAwardedToReferred);
    }

    /// <summary>
    /// Deactivated is not frozen: only a company whose archive is requested refuses writes to its books, so an
    /// inviter whose company is deactivated, with no archive requested, is still paid the referral credit on it.
    /// </summary>
    [Fact]
    public async Task A_Deactivated_Inviters_Company_Not_Frozen_For_Archive_Still_Pays_The_Inviter()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: CzkId, referrerCompany: TestTenants.Second);
        await DeactivateAsync(TestTenants.Second, requestArchive: false);
        await using (var ctx = NewContext())
        {
            var company = await ctx.Tenants.AsNoTracking().SingleAsync(t => t.Id == TestTenants.Second);
            Assert.False(company.IsActive);
            Assert.False(company.IsFrozen);
        }

        await CompleteAsync(referredId);

        var inviter = Assert.Single(await AccountsAsync(referrerId));
        Assert.Equal(TestTenants.Second, inviter.TenantId);
        Assert.Equal(150m, inviter.Balance);
        Assert.Equal(150m, Assert.Single(await AccountsAsync(referredId)).Balance);
        var referral = await ReferralAsync(referralId);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(150m, referral.CreditAwardedToReferrer);
        Assert.Equal(150m, referral.CreditAwardedToReferred);
    }

    /// <summary>
    /// The force-qualify path through the same grant, with the friend's existing account on a frozen
    /// company's books: the inviter's active company commits, the inviter is paid, and the frozen account is
    /// left exactly as it was.
    /// </summary>
    [Fact]
    public async Task Force_Qualify_Leaves_An_Account_On_A_Frozen_Companys_Books_Untouched_And_Pays_The_Other_Side()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: null, referredCompany: TestTenants.Second);
        await using (var ctx = NewContext())
        {
            var held = CreditAccount.Create(referredId, CzkId, "seed");
            held.TenantId = TestTenants.Second;
            ctx.CreditAccounts.Add(held);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await FreezeAsync(TestTenants.Second);

        var response = await ForceQualifyAsync(referralId);

        Assert.Equal(new ForceQualifyReferral.Response(referralId, 150m, "CZK", 0m, "CZK"), response);
        Assert.Equal(150m, Assert.Single(await AccountsAsync(referrerId)).Balance);
        var frozen = Assert.Single(await AccountsAsync(referredId));
        Assert.Equal(0m, frozen.Balance);
        Assert.Empty(frozen.Transactions);
        var referral = await ReferralAsync(referralId);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(150m, referral.CreditAwardedToReferrer);
        Assert.Null(referral.CreditAwardedToReferred);
    }

    [Fact]
    public async Task Force_Qualify_Credits_Each_Side_In_The_Currency_It_Books_In()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: EurId, referrerOrderCurrencyId: CzkId, completed: false);

        var response = await ForceQualifyAsync(referralId);

        Assert.Equal(new ForceQualifyReferral.Response(referralId, 150m, "CZK", 6m, "EUR"), response);
        Assert.Equal(CzkId, Assert.Single(await AccountsAsync(referrerId)).CurrencyId);
        Assert.Equal(EurId, Assert.Single(await AccountsAsync(referredId)).CurrencyId);
        Assert.Equal(ReferralStatus.Qualified, (await ReferralAsync(referralId)).Status);
    }

    [Fact]
    public async Task Force_Qualify_For_A_Friend_Who_Never_Booked_Pays_In_The_Platform_Default()
    {
        await ResetAsync();
        var (referrerId, _, referralId) = await SeedAsync(orderCurrencyId: null);

        var response = await ForceQualifyAsync(referralId);

        Assert.Equal(new ForceQualifyReferral.Response(referralId, 150m, "CZK", 150m, "CZK"), response);
        Assert.Equal(CzkId, Assert.Single(await AccountsAsync(referrerId)).CurrencyId);
    }

    [Fact]
    public async Task Reverse_Takes_Back_Both_Grants_In_Full()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: CzkId);
        await CompleteAsync(referredId);

        var response = await ReverseAsync(referralId);

        Assert.Equal(new ReverseReferral.Response(referralId, 150m, "CZK", 150m, "CZK"), response);
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
    public async Task Reverse_Of_A_Cross_Currency_Referral_Takes_Each_Grant_Back_In_Its_Own_Currency()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: EurId, referrerOrderCurrencyId: CzkId);
        await CompleteAsync(referredId);

        var response = await ReverseAsync(referralId);

        Assert.Equal(new ReverseReferral.Response(referralId, 150m, "CZK", 6m, "EUR"), response);
        Assert.Equal(0m, Assert.Single(await AccountsAsync(referrerId)).Balance);
        Assert.Equal(0m, Assert.Single(await AccountsAsync(referredId)).Balance);
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

        Assert.Equal(new ReverseReferral.Response(referralId, 150m, "CZK", 50m, "CZK"), response);
        var spent = Assert.Single(await AccountsAsync(referredId));
        Assert.Equal(0m, spent.Balance);
        Assert.Equal(-50m, Assert.Single(spent.Transactions, t => t.Reason == CreditTransactionReason.ReferralReversed).Amount);
        Assert.Equal(0m, Assert.Single(await AccountsAsync(referrerId)).Balance);
    }

    /// <summary>
    /// The inviter booked the home with their company, the friend with another: each company has its own row
    /// for it, typed differently. The friend's first completion is held, nobody is paid, and the support staff
    /// of the referral's company — not the ambient one — are told.
    /// </summary>
    [Fact]
    public async Task One_Home_Booked_With_Two_Companies_Holds_The_Referral_And_Tells_The_Referrals_Company()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: CzkId,
            referrerCompany: TestTenants.Second,
            referrerOrderCurrencyId: CzkId,
            referrerStreet: "Vinohradská 12",
            referrerZip: "120 00",
            friendStreet: "vinohradska 12",
            friendZip: "12000");
        await SeedSupportAsync(TestTenants.Second);
        await using (var ctx = NewContext())
        {
            var addressIds = await ctx.Orders.IgnoreQueryFilters()
                .Where(o => o.Id == OrderId || o.Id == ReferrerOrderId)
                .Select(o => o.CustomerAddressId)
                .ToListAsync();
            Assert.Equal(2, addressIds.Distinct().Count());
        }

        await CompleteAsync(referredId);

        var referral = await ReferralAsync(referralId);
        Assert.Equal(ReferralStatus.Accepted, referral.Status);
        Assert.Equal(Referral.HoldReasonAddress, referral.HoldReasons);
        Assert.Equal(OrderId, referral.FirstQualifyingOrderId);
        Assert.Empty(await AccountsAsync(referrerId));
        Assert.Empty(await AccountsAsync(referredId));

        await using var verify = NewContext();
        var told = Assert.Single(await verify.Set<UserNotification>().IgnoreQueryFilters()
            .Where(n => n.EventKey == AdminNotificationEventCatalog.ReferralHeld).ToListAsync());
        Assert.Equal(TestTenants.Second, told.TenantId);
        Assert.Contains(referralId, told.ArgsJson, StringComparison.Ordinal);
        var email = Assert.Single(await verify.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.SendEmail).ToListAsync());
        Assert.Equal(TestTenants.Second, email.TenantId);
    }

    /// <summary>
    /// A saved address the inviter deleted is not theirs any more: it is not compared. The same address still
    /// saved is.
    /// </summary>
    [Theory]
    [InlineData(true, ReferralStatus.Accepted)]
    [InlineData(false, ReferralStatus.Qualified)]
    public async Task Only_A_Saved_Address_The_Inviter_Still_Keeps_Is_Compared(bool stillSaved, ReferralStatus expected)
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: CzkId);
        await using (var ctx = NewContext())
        {
            var home = NewAddress("123 Main St", "110 00", TestTenants.Default);
            ctx.Addresses.Add(home);
            var saved = SavedAddress.Create(referrerId, home.Id, "Home", isDefault: true);
            saved.TenantId = TestTenants.Default;
            saved.IsActive = stillSaved;
            ctx.SavedAddresses.Add(saved);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await CompleteAsync(referredId);

        var referral = await ReferralAsync(referralId);
        Assert.Equal(expected, referral.Status);
        Assert.Equal(stillSaved ? Referral.HoldReasonAddress : null, referral.HoldReasons);
        Assert.Equal(stillSaved ? 0 : 1, (await AccountsAsync(referrerId)).Count);
    }

    [Theory]
    [InlineData(true, ReferralStatus.Accepted)]
    [InlineData(false, ReferralStatus.Expired)]
    public async Task The_Expiry_Sweep_Leaves_A_Held_Referral_Waiting(bool held, ReferralStatus expected)
    {
        await ResetAsync();
        var (_, _, referralId) = await SeedAsync(orderCurrencyId: CzkId);
        if (held)
        {
            await HoldAsync(referralId);
        }

        await using (var ctx = NewContext())
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "Referrals" SET "AcceptedOn" = now() - interval '100 days' WHERE "Id" = {referralId}""");
            await Service(ctx).ExpireStaleReferralsAsync(CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.Equal(expected, (await ReferralAsync(referralId)).Status);
    }

    [Fact]
    public async Task Releasing_A_Held_Referral_Pays_Both_Sides_Against_The_Held_Order()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: EurId);
        await HoldAsync(referralId);

        var response = await ForceQualifyAsync(referralId, expectHeld: true);

        Assert.Equal(new ForceQualifyReferral.Response(referralId, 6m, "EUR", 6m, "EUR"), response);
        foreach (var userId in new[] { referrerId, referredId })
        {
            var grant = Assert.Single(Assert.Single(await AccountsAsync(userId)).Transactions);
            Assert.Equal(OrderId, grant.OrderId);
        }

        var referral = await ReferralAsync(referralId);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(OrderId, referral.FirstQualifyingOrderId);
        Assert.Equal(Referral.HoldReasonAddress, referral.HoldReasons);
    }

    [Fact]
    public async Task Rejecting_A_Held_Referral_Pays_Nothing_And_Ends_It()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: CzkId);
        await HoldAsync(referralId);

        var response = await ReverseAsync(referralId, expectHeld: true);

        Assert.Equal(new ReverseReferral.Response(referralId, 0m, null, 0m, null), response);
        Assert.Empty(await AccountsAsync(referrerId));
        Assert.Empty(await AccountsAsync(referredId));
        Assert.Equal(ReferralStatus.Reversed, (await ReferralAsync(referralId)).Status);
    }

    /// <summary>
    /// Two administrators act on one held referral at once: one releases it, the other rejects it, each having
    /// read it held. Whichever commits second conflicts on the row's version and rolls back, so the row never
    /// ends rejected with both grants standing.
    /// </summary>
    [Fact]
    public async Task A_Release_And_A_Rejection_Of_One_Held_Referral_Cannot_Both_Commit()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: CzkId);
        await HoldAsync(referralId);

        await using var releasing = NewContext();
        await using var rejecting = NewContext();
        var released = await ForceQualifyHandler(releasing)
            .Handle(new ForceQualifyReferral.Command(referralId, Reason, ExpectHeld: true), CancellationToken.None);
        var rejected = await ReverseHandler(rejecting)
            .Handle(new ReverseReferral.Command(referralId, Reason, ExpectHeld: true), CancellationToken.None);
        Assert.True(released.IsSuccess, released.Error?.Message);
        Assert.True(rejected.IsSuccess, rejected.Error?.Message);

        await releasing.CommitAsync(CancellationToken.None);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => rejecting.CommitAsync(CancellationToken.None));

        Assert.Equal(ReferralStatus.Qualified, (await ReferralAsync(referralId)).Status);
        var ledger = (await AccountsAsync(referrerId)).Concat(await AccountsAsync(referredId))
            .SelectMany(a => a.Transactions).ToList();
        Assert.Equal(2, ledger.Count(t => t.Reason == CreditTransactionReason.Referral));
        Assert.DoesNotContain(ledger, t => t.Reason == CreditTransactionReason.ReferralReversed);
    }

    /// <summary>
    /// One administrator releases a held referral while another has the rejection dialog open on the same held
    /// row. The release commits first and pays both sides; the rejection, sent for a held row, is refused
    /// instead of taking back grants nobody chose to take back.
    /// </summary>
    [Fact]
    public async Task A_Rejection_Sent_From_The_Held_Row_After_It_Was_Released_Is_Refused_And_The_Grants_Stand()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(orderCurrencyId: CzkId);
        await HoldAsync(referralId);
        await ForceQualifyAsync(referralId, expectHeld: true);

        await using (var ctx = NewContext())
        {
            var rejected = await ReverseHandler(ctx)
                .Handle(new ReverseReferral.Command(referralId, Reason, ExpectHeld: true), CancellationToken.None);
            Assert.True(rejected.IsFailure);
            Assert.Equal(BusinessErrorMessage.ReferralHoldChanged, rejected.Error!.Message);
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.Equal(ReferralStatus.Qualified, (await ReferralAsync(referralId)).Status);
        Assert.Equal(150m, Assert.Single(await AccountsAsync(referrerId)).Balance);
        Assert.Equal(150m, Assert.Single(await AccountsAsync(referredId)).Balance);
    }

    /// <summary>
    /// The force-qualify dialog was opened on a row that was not held; the friend's first order completed on a
    /// shared home before it was sent. The force-qualify is refused, so the hold's reason is not paid past unseen.
    /// </summary>
    [Fact]
    public async Task A_Force_Qualify_Sent_From_A_Row_Not_Yet_Held_Is_Refused_Once_The_Referral_Is_Held()
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: CzkId, referrerOrderCurrencyId: CzkId, referrerStreet: "123 Main St");
        await CompleteAsync(referredId);

        await using (var ctx = NewContext())
        {
            var forced = await ForceQualifyHandler(ctx)
                .Handle(new ForceQualifyReferral.Command(referralId, Reason, ExpectHeld: false), CancellationToken.None);
            Assert.True(forced.IsFailure);
            Assert.Equal(BusinessErrorMessage.ReferralHoldChanged, forced.Error!.Message);
            await ctx.CommitAsync(CancellationToken.None);
        }

        var referral = await ReferralAsync(referralId);
        Assert.Equal(ReferralStatus.Accepted, referral.Status);
        Assert.Equal(Referral.HoldReasonAddress, referral.HoldReasons);
        Assert.Empty(await AccountsAsync(referrerId));
        Assert.Empty(await AccountsAsync(referredId));
    }

    /// <summary>
    /// A shared home on a referral whose friend books in złoty, which has no referral figure. When the inviter
    /// books in złoty too, neither side can be paid: the referral qualifies with nothing paid and support is not
    /// told. When the inviter books in koruna, the inviter could be paid, so the referral is held and support is.
    /// </summary>
    [Theory]
    [InlineData(PlnId, false)]
    [InlineData(CzkId, true)]
    public async Task A_Shared_Home_Is_Held_Only_When_Either_Side_Could_Be_Paid(string inviterCurrencyId, bool held)
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: PlnId, referrerOrderCurrencyId: inviterCurrencyId, referrerStreet: "123 Main St");
        await SeedSupportAsync(TestTenants.Default);

        await CompleteAsync(referredId);

        var referral = await ReferralAsync(referralId);
        Assert.Equal(held ? ReferralStatus.Accepted : ReferralStatus.Qualified, referral.Status);
        Assert.Equal(held ? Referral.HoldReasonAddress : null, referral.HoldReasons);
        Assert.Null(referral.CreditAwardedToReferrer);
        Assert.Null(referral.CreditAwardedToReferred);
        Assert.Equal(held ? 1 : 0, await ReferralHeldNoticesAsync());
        Assert.Empty(await AccountsAsync(referrerId));
        Assert.Empty(await AccountsAsync(referredId));
    }

    /// <summary>
    /// One side books in złoty, which has no referral figure, the other in koruna. The koruna side is paid and
    /// the referral qualifies, whichever side it is — and so whichever side the ordinal lock order reaches first.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Side_Booking_In_A_Currency_With_No_Figure_Is_Paid_Nothing_And_The_Other_Side_Is_Paid(bool inviterLacksTheFigure)
    {
        await ResetAsync();
        var (referrerId, referredId, referralId) = await SeedAsync(
            orderCurrencyId: inviterLacksTheFigure ? CzkId : PlnId,
            referrerOrderCurrencyId: inviterLacksTheFigure ? PlnId : CzkId);

        await CompleteAsync(referredId);

        var paid = Assert.Single(await AccountsAsync(inviterLacksTheFigure ? referredId : referrerId));
        Assert.Equal(CzkId, paid.CurrencyId);
        Assert.Equal(150m, paid.Balance);
        Assert.Empty(await AccountsAsync(inviterLacksTheFigure ? referrerId : referredId));
        var referral = await ReferralAsync(referralId);
        Assert.Equal(ReferralStatus.Qualified, referral.Status);
        Assert.Equal(inviterLacksTheFigure ? null : 150m, referral.CreditAwardedToReferrer);
        Assert.Equal(inviterLacksTheFigure ? 150m : null, referral.CreditAwardedToReferred);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Currency_One_Side_Of_A_Referral_Was_Paid_In_Is_In_Use(bool referrerSide)
    {
        await ResetAsync();
        var (_, _, referralId) = await SeedAsync(orderCurrencyId: null);
        await using (var ctx = NewContext())
        {
            Assert.False(await new CurrencyRepository(ctx).IsInUseAsync(PlnId, CancellationToken.None));
            var referral = await ctx.Referrals.IgnoreQueryFilters().SingleAsync(r => r.Id == referralId);
            referral.ForceQualify(
                referrerCurrencyId: PlnId, creditToReferrer: referrerSide ? 10m : null,
                referredCurrencyId: PlnId, creditToReferred: referrerSide ? null : 10m,
                actorId: AdminId);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var verify = NewContext();
        Assert.True(await new CurrencyRepository(verify).IsInUseAsync(PlnId, CancellationToken.None));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
