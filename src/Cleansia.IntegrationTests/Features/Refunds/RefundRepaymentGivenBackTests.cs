using System.Reflection;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Features.Reports;
using Cleansia.Core.AppServices.Features.Reports.DTOs;
using Cleansia.Core.AppServices.Features.Reports.Filters;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Refunds;

/// <summary>
/// A failed refund repaid by hand, through the pipeline on Postgres with the real refund seam, loyalty
/// service and credit repository: the repayment is staged, not committed, when the clawback and the
/// goodwill credit run, which the unit tests' mocks cannot see.
/// </summary>
[Collection("PostgresCollection")]
public sealed class RefundRepaymentGivenBackTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "ord-repaid";
    private const string UserId = "user-repaid";
    private const string CurrencyId = "cur-repaid";
    private static readonly string FailedKey = RefundService.BuildRefundKey(
        new RefundRequest(OrderId, 600m, RefundReason.ServiceNotRendered, "admin", RefundRequestId: "lines-repaid"));

    private static IUserSessionProvider Session() => new TestUserSessionProvider("admin-repaid", "admin@cleansia.test",
        [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);

    private CleansiaDbContext NewContext() => new(new DbContextOptionsBuilder<CleansiaDbContext>()
        .UseNpgsql(Fixture.GetConnectionString()).Options, Session(), new FixedTenantProvider(TestTenants.Default));

    // An order of 1,000 that took 250 credit, whose partial refund (300 card, a 400 slice, so a 100 credit share)
    // Stripe failed, repaid as credit: the customer holds the 100 share back plus 300 goodwill.
    [Fact]
    public async Task A_Failed_Refund_On_An_Order_That_Took_Credit_Repaid_As_Credit_Keeps_Both_Credits_On_The_Balance_And_Counts_Once_In_Revenue()
    {
        var failedId = await ResetAndSeedAsync(creditApplied: 250m, failedAmount: 300m, earlierRefund: false);

        var result = await RepayAsync(failedId, RefundRepaymentMethod.Credit, null);

        Assert.True(result.IsSuccess, result.Error?.Message);
        await using var read = NewContext();
        var ledger = await read.CreditTransactions.IgnoreQueryFilters().Where(t => t.OrderId == OrderId)
            .Select(t => new { t.Reason, t.Amount }).ToListAsync();
        Assert.Equal(100m, ledger.Where(t => t.Reason == CreditTransactionReason.OrderPaymentReturned).Sum(t => t.Amount));
        Assert.Equal(300m, ledger.Where(t => t.Reason == CreditTransactionReason.Goodwill).Sum(t => t.Amount));
        var account = await read.CreditAccounts.IgnoreQueryFilters().SingleAsync(a => a.UserId == UserId);
        Assert.Equal(400m, account.Balance);

        // The goodwill is not a returned payment: the refund row already counts the 300 as given back.
        var report = await RevenueReportAsync(read);
        Assert.Equal((300m, 100m, 600m), (report.TotalRefundedToCard, report.TotalReturnedToCredit, report.NetRevenue));
    }

    private async Task<BusinessResult<RecordRefundRepayment.Response>> RepayAsync(
        string refundId, RefundRepaymentMethod method, string? reference)
    {
        await using var ctx = NewContext();
        var orders = new OrderRepository(ctx);
        var refunds = new RefundRepository(ctx);
        var credit = new CreditAccountRepository(ctx);
        var seam = new RefundService(refunds, orders, credit, Mock.Of<IStripeClientFactory>(), Mock.Of<IAdminNotifier>(),
            new UserNotificationRepository(ctx), NullLogger<RefundService>.Instance);
        var loyalty = new LoyaltyService(orders, new LoyaltyAccountRepository(ctx), new LoyaltyTierConfigRepository(ctx),
            new LoyaltyTransactionRepository(ctx), new CurrencyRepository(ctx), refunds, credit,
            Mock.Of<INotificationProducer>(), NullLogger<LoyaltyService>.Instance);
        var handler = new RecordRefundRepayment.Handler(refunds, orders, credit, seam, loyalty, Session());
        var command = new RecordRefundRepayment.Command(OrderId, refundId, method, reference);
        return await new UnitOfWorkPipelineBehavior<RecordRefundRepayment.Command, BusinessResult<RecordRefundRepayment.Response>>(ctx)
            .Handle(command, ct => handler.Handle(command, ct), CancellationToken.None);
    }

    /// <summary><c>GetRevenueReport.Handler</c> is internal and no project has InternalsVisibleTo, so it is built reflectively.</summary>
    private static async Task<RevenueReportDto> RevenueReportAsync(CleansiaDbContext ctx)
    {
        var handlerType = typeof(GetRevenueReport).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, new OrderRepository(ctx), new RefundRepository(ctx),
            new CreditAccountRepository(ctx), new CurrencyRepository(ctx))!;
        var query = new GetRevenueReport.Query(new ReportFilter(DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(1), CurrencyId));
        var result = await (Task<BusinessResult<RevenueReportDto>>)handlerType.GetMethod("Handle")!
            .Invoke(handler, [query, CancellationToken.None])!;
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private async Task<string> ResetAndSeedAsync(decimal creditApplied = 0m, decimal failedAmount = 600m, bool earlierRefund = true)
    {
        await using (var conn = new NpgsqlConnection(Fixture.GetConnectionString()))
        {
            await conn.OpenAsync();
            var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions { DbAdapter = DbAdapter.Postgres,
                SchemasToExclude = ["pg_catalog", "information_schema"] });
            await respawner.ResetAsync(conn);
            await SeedTenantRegistryAsync(conn);
        }

        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZ", "CZ", true);
        country.Id = "country-repaid";
        ctx.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        ctx.Currencies.Add(currency);
        var user = User.CreateWithPassword("repaid@cleansia.test", "Password123!", "Repaid", "Customer");
        user.Id = UserId;
        ctx.Users.Add(user);
        var order = Order.Create("Repaid Customer", user.Email, "+420000000000", Address.Create("Test St 1", "Brno", "60200", country.Id),
            1, 1, DateTime.UtcNow.AddDays(-1), PaymentType.Card, 1000m, currency.Id,
            earlierRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Paid, userId: UserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        if (creditApplied > 0m) order.ApplyCredit(creditApplied, UserId);
        order.AssignStripePaymentIntentId("pi_repaid");
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.CompleteOrder(60);
        ctx.Orders.Add(order);
        var account = LoyaltyAccount.Create(UserId);
        account.TenantId = TestTenants.Default;
        account.GrantPoints(100, LoyaltyEarnSource.OrderCompleted, OrderId, "seed", new LoyaltyTierThresholds(1000, 2000, 3000));
        ctx.LoyaltyAccounts.Add(account);
        if (earlierRefund)
        {
            ctx.Refunds.Add(Refund.Create(OrderId, "earlier-repaid", 100m, "CZK",
                RefundReason.ServiceNotRendered, RefundSource.AppRefund).MarkSucceeded(null, DateTimeOffset.UtcNow));
        }

        if (creditApplied > 0m)
        {
            var creditAccount = CreditAccount.Create(UserId, CurrencyId, UserId);
            creditAccount.TenantId = TestTenants.Default;
            ctx.CreditAccounts.Add(creditAccount);
        }

        var failed = Refund.Create(OrderId, FailedKey, failedAmount, "CZK", RefundReason.ServiceNotRendered, RefundSource.AppRefund);
        failed.MarkFailedByStripe("re_repaid", DateTimeOffset.UtcNow);
        ctx.Refunds.Add(failed);
        await ctx.CommitAsync(CancellationToken.None);
        return failed.Id;
    }

    private sealed class FixedTenantProvider(string tenant) : ITenantProvider
    {
        private string? _tenant = tenant;
        public string? GetCurrentTenantId() => _tenant;
        public void SetTenantOverride(string value) => _tenant = value;
        public void ClearTenantOverride() => _tenant = null;
    }
}
