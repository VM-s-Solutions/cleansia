using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Refunds;

/// <summary>
/// A complaint on the order was settled in credit before anyone refunded it. Against real Postgres with
/// the real refund seam, a later refund is held to what the sale has left, so the card refunds, the
/// credit returned and the settlement never add up to more than the price.
/// </summary>
[Collection("PostgresCollection")]
public class RefundAfterCreditSettlementTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-settled-refund";
    private const string CzkId = "cur-czk-settled-refund";
    private const string OrderId = "order-settled-refund";
    private const string PaymentIntentId = "pi_settled_refund";
    private const string FullKey = $"refund:{OrderId}:admin:full";

    private readonly Mock<IStripeClient> _stripe = new();

    private CleansiaDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .AddInterceptors(interceptors)
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

    /// <summary>
    /// A paid card order, with <paramref name="creditApplied"/> spent on it at checkout under its payment
    /// key, and an earlier complaint on it settled in <paramref name="settledInCredit"/> of credit.
    /// </summary>
    private async Task<string> SeedAsync(decimal total, decimal creditApplied, decimal settledInCredit)
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
            var user = User.CreateWithPassword("settled-refund@cleansia.test", "Seed-Password-123", "Settled", "Refund");
            ctx.Users.Add(user);
            await ctx.CommitAsync(CancellationToken.None);
            userId = user.Id;
        }

        await using (var ctx = NewContext())
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserAsync(userId, CzkId, CancellationToken.None);
            if (creditApplied > 0m)
            {
                account!.Issue(creditApplied, CreditTransactionReason.Goodwill, "seed-grant", "seed", note: "n");
            }

            account!.Issue(
                settledInCredit, CreditTransactionReason.DisputeSettlement, "dispute-settlement:dispute-earlier", "admin",
                orderId: OrderId, disputeId: "dispute-earlier");

            var order = Order.Create(
                customerName: "Settled Refund",
                customerEmail: "settled-refund@cleansia.test",
                customerPhone: "+420000000002",
                customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddDays(-1),
                paymentType: PaymentType.Card,
                totalPrice: total,
                currencyId: CzkId,
                paymentStatus: PaymentStatus.Paid,
                userId: userId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            order.ApplyCredit(creditApplied, userId);
            order.AssignStripePaymentIntentId(PaymentIntentId);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
            ctx.Orders.Add(order);
            await ctx.CommitAsync(CancellationToken.None);

            if (creditApplied > 0m)
            {
                Assert.True(await new CreditAccountRepository(ctx).TryDebitAsync(
                    account.Id, creditApplied, CreditTransactionReason.OrderPayment, $"order-payment-{OrderId}", userId,
                    CancellationToken.None, orderId: OrderId));
            }
        }

        return userId;
    }

    private async Task<BusinessResult<RefundResult>> RefundAsync(
        decimal amount, string refundRequestId, params IInterceptor[] interceptors)
    {
        await using var ctx = NewContext(interceptors);
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        var service = new RefundService(
            new RefundRepository(ctx),
            new OrderRepository(ctx),
            new CreditAccountRepository(ctx),
            factory.Object,
            NullLogger<RefundService>.Instance);
        return await service.IssueRefundAsync(
            new RefundRequest(OrderId, amount, RefundReason.AdminDiscretion, "admin", RefundRequestId: refundRequestId),
            CancellationToken.None);
    }

    private sealed record GivenBack(
        decimal Card, decimal CreditReturned, decimal SettledInCredit, decimal Balance, decimal LedgerSum, PaymentStatus PaymentStatus)
    {
        public decimal Total => Card + CreditReturned + SettledInCredit;
    }

    private async Task<GivenBack> ReadAsync(string userId)
    {
        await using var ctx = NewContext();
        var card = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OrderId == OrderId && r.Status == RefundStatus.Succeeded)
            .SumAsync(r => r.Amount);
        var returned = await ctx.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.OrderPaymentReturned)
            .SumAsync(t => t.Amount);
        var settled = await ctx.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.DisputeSettlement)
            .SumAsync(t => t.Amount);
        var account = await ctx.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        return new GivenBack(card, returned, settled, account.Balance, account.Transactions.Sum(t => t.Amount), order.PaymentStatus);
    }

    [Fact]
    public async Task A_Full_Refund_After_A_Complaint_Settled_In_Credit_Sends_The_Card_Only_What_Is_Left()
    {
        await ResetAsync();
        var userId = await SeedAsync(total: 1000m, creditApplied: 0m, settledInCredit: 300m);

        var result = await RefundAsync(1000m, "full");

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(700m, result.Value!.Amount);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, 700m, FullKey, It.IsAny<CancellationToken>()), Times.Once);
        _stripe.VerifyNoOtherCalls();
        await using (var ctx = NewContext())
        {
            var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync();
            Assert.Equal(700m, refund.Amount);
            Assert.Equal(RefundStatus.Succeeded, refund.Status);
        }

        var given = await ReadAsync(userId);
        Assert.Equal(1000m, given.Total);
        Assert.Equal(300m, given.SettledInCredit);
        Assert.Equal(0m, given.CreditReturned);
        Assert.Equal(PaymentStatus.PartiallyRefunded, given.PaymentStatus);
    }

    /// <summary>
    /// 2000 paid 500 in credit and 1500 by card, with 400 already settled in credit: 1600 is left, and it
    /// goes back in the mix it was paid in, 1200 to the card and 400 to the balance.
    /// </summary>
    [Fact]
    public async Task A_Card_And_Credit_Order_Refunded_After_A_Settlement_Returns_Both_Legs_In_Proportion_And_No_More()
    {
        await ResetAsync();
        var userId = await SeedAsync(total: 2000m, creditApplied: 500m, settledInCredit: 400m);

        var result = await RefundAsync(2000m, "full");

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1200m, result.Value!.Amount);
        Assert.Equal(400m, result.Value.CreditReturned);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, 1200m, FullKey, It.IsAny<CancellationToken>()), Times.Once);

        var given = await ReadAsync(userId);
        Assert.Equal(1200m, given.Card);
        Assert.Equal(400m, given.CreditReturned);
        Assert.Equal(2000m, given.Total);
        Assert.Equal(800m, given.Balance);
        Assert.Equal(given.LedgerSum, given.Balance);
    }

    /// <summary>
    /// A partial refund of 600 takes 450 card and 150 credit. The full refund after it finds 1000 left of
    /// the 2000 sale: 750 card and 250 credit. Nothing is left for a third.
    /// </summary>
    [Fact]
    public async Task A_Partial_Then_A_Full_Refund_After_A_Settlement_Never_Pass_The_Price()
    {
        await ResetAsync();
        var userId = await SeedAsync(total: 2000m, creditApplied: 500m, settledInCredit: 400m);

        var partial = await RefundAsync(600m, "partial-1");
        var full = await RefundAsync(2000m, "full");
        var third = await RefundAsync(100m, "partial-2");

        Assert.True(partial.IsSuccess, partial.Error?.Message);
        Assert.Equal(450m, partial.Value!.Amount);
        Assert.Equal(150m, partial.Value.CreditReturned);
        Assert.True(full.IsSuccess, full.Error?.Message);
        Assert.Equal(750m, full.Value!.Amount);
        Assert.Equal(250m, full.Value.CreditReturned);
        Assert.True(third.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, third.Error!.Message);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        var given = await ReadAsync(userId);
        Assert.Equal(1200m, given.Card);
        Assert.Equal(400m, given.CreditReturned);
        Assert.Equal(2000m, given.Total);
        Assert.Equal(given.LedgerSum, given.Balance);
    }

    /// <summary>
    /// The admin's full refund of the 2000 sale above went through at Stripe for 1200 and its 400 credit leg
    /// came back, but the commit recording it was lost. The retry finds the row still pending and sends Stripe
    /// the same 1200 on the same key, which Stripe answers with the refund it already made; a smaller amount
    /// would be refused on that key every time. 1200 + 400 + 400 is the price.
    /// </summary>
    [Fact]
    public async Task A_Full_Refund_Retried_After_Its_Record_Was_Lost_Sends_Stripe_The_Same_Amount()
    {
        await ResetAsync();
        var userId = await SeedAsync(total: 2000m, creditApplied: 500m, settledInCredit: 400m);
        var stripeCalls = new List<decimal>();
        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), FullKey, It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, CancellationToken>((_, amount, _, _) => stripeCalls.Add(amount))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<DbUpdateException>(() => RefundAsync(2000m, "full", new LoseTheSucceededRecord()));

        await using (var ctx = NewContext())
        {
            var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync();
            Assert.Equal(RefundStatus.Pending, refund.Status);
            Assert.Equal(1200m, refund.Amount);
        }

        var retried = await RefundAsync(2000m, "full");

        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Equal([1200m, 1200m], stripeCalls);
        var given = await ReadAsync(userId);
        Assert.Equal(1200m, given.Card);
        Assert.Equal(400m, given.CreditReturned);
        Assert.Equal(2000m, given.Total);
        Assert.Equal(given.LedgerSum, given.Balance);
    }

    private sealed class LoseTheSucceededRecord : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Refund>()
                .Any(e => e.State == EntityState.Modified && e.Entity.Status == RefundStatus.Succeeded))
            {
                throw new DbUpdateException("The connection was lost before the refund was recorded.");
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
