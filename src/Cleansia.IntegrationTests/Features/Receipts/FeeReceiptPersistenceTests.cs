using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Receipts;

/// <summary>
/// More than one receipt per order, on real Postgres: an order holds its sale receipt and a fee receipt
/// for a receivable paid on it, and reads the sale receipt as its own; one receivable earns one fee receipt;
/// and a registered fee receipt does not stand in for the sale receipt the reconciliation sweep is owed.
/// The receivable reads the sweep and the customer use cross companies, and the sweep's leaves out a frozen
/// company, whose books would refuse the charge attempt's write.
/// </summary>
[Collection("PostgresCollection")]
public class FeeReceiptPersistenceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string LanguageId = "language-en-fee";
    private const string CountryId = "country-cz-fee";
    private const string CurrencyId = "currency-czk-fee";
    private const string CustomerEmail = "fee-owner@cleansia.test";

    private string _orderId = "";
    private string _receivableId = "";
    private string _customerId = "";

    [Fact]
    public async Task An_Order_Holds_Its_Sale_Receipt_And_A_Fee_Receipt_And_Reads_The_Sale_As_Its_Receipt()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var (order, receivable) = Seed(ctx);
                ctx.OrderReceipts.AddRange(
                    OrderReceipt.Create(order.Id, "RCP-FEE-0001", "receipt.pdf", "2026/ORD/receipt.pdf", LanguageId),
                    OrderReceipt.CreateFee(receivable, "RCP-FEE-0002", "fee.pdf", "2026/ORD/fee.pdf", LanguageId));
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var order = await provider.GetRequiredService<IOrderRepository>()
                    .GetByIdIgnoringTenantAsync(_orderId, CancellationToken.None);
                return (Sale: order!.Receipt?.ReceiptNumber, Count: order.Receipts.Count,
                    Fee: order.Receipts.Single(r => r.IsFee).ReceivableId);
            },
            assert: (CleansiaDbContext _, (string? Sale, int Count, string? Fee) r) =>
            {
                Assert.Equal(("RCP-FEE-0001", 2, _receivableId), (r.Sale, r.Count, r.Fee));
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Receivable_Earns_One_Fee_Receipt()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var (_, receivable) = Seed(ctx);
                ctx.OrderReceipts.Add(OrderReceipt.CreateFee(receivable, "RCP-FEE-0003", "fee.pdf", "2026/ORD/fee-1.pdf", LanguageId));
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var ctx = provider.GetRequiredService<CleansiaDbContext>();
                var receivable = await ctx.Receivables.IgnoreQueryFilters().SingleAsync(r => r.Id == _receivableId);
                ctx.OrderReceipts.Add(OrderReceipt.CreateFee(receivable, "RCP-FEE-0004", "fee.pdf", "2026/ORD/fee-2.pdf", LanguageId));
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));
                return (ex.InnerException as PostgresException)?.SqlState;
            },
            assert: (CleansiaDbContext _, string? sqlState) =>
            {
                Assert.Equal(PostgresErrorCodes.UniqueViolation, sqlState);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Registered_Fee_Receipt_Does_Not_Stand_In_For_The_Sale_Receipt_The_Sweep_Is_Owed()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var (_, receivable) = Seed(ctx);
                var fee = OrderReceipt.CreateFee(receivable, "RCP-FEE-0005", "fee.pdf", "2026/ORD/fee.pdf", LanguageId);
                fee.SetFiscalData("eet", "FIK-FEE-1", DateTime.UtcNow);
                ctx.OrderReceipts.Add(fee);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var candidates = await provider.GetRequiredService<IOrderRepository>()
                    .GetReceiptReconciliationCandidatesAsync(DateTime.UtcNow.AddMinutes(5), 50, CancellationToken.None);
                return candidates.Select(o => o.Id).ToList();
            },
            assert: (CleansiaDbContext _, List<string> ids) =>
            {
                Assert.Contains(_orderId, ids);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task The_Sweep_Batch_And_The_Customers_Own_List_Read_Across_Companies()
    {
        string attempted = "", paid = "", elsewhere = "";
        await TestMethod(
            arrange: async ctx =>
            {
                var (order, _) = Seed(ctx, paid: false);
                var tried = Receivable.ForCashCancellationFee(order, 100m);
                tried.RecordChargeAttempt();
                var settled = Receivable.ForCashCancellationFee(order, 200m);
                settled.MarkPaid("pi_settled", DateTimeOffset.UtcNow);
                var otherCompanys = Receivable.ForCashCancellationFee(order, 300m);
                otherCompanys.TenantId = TestTenants.Second;
                ctx.Receivables.AddRange(tried, settled, otherCompanys);
                (attempted, paid, elsewhere) = (tried.Id, settled.Id, otherCompanys.Id);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var repository = provider.GetRequiredService<IReceivableRepository>();
                var batch = await repository.GetUnchargedOpenIgnoringTenantAsync(50, CancellationToken.None);
                var mine = await repository.GetOpenForUserAsync(_customerId, CancellationToken.None);
                return (
                    Batch: batch.Select(r => r.Id).ToHashSet(),
                    Mine: mine.Select(r => r.Id).ToHashSet(),
                    Loaded: mine.All(r => r.Order?.DisplayOrderNumber is not null && r.Currency?.Code == "CZK"));
            },
            assert: (CleansiaDbContext _, (HashSet<string> Batch, HashSet<string> Mine, bool Loaded) r) =>
            {
                Assert.Equal(new HashSet<string> { _receivableId, elsewhere }, r.Batch);
                Assert.Equal(new HashSet<string> { _receivableId, attempted, elsewhere }, r.Mine);
                Assert.DoesNotContain(paid, r.Mine);
                Assert.True(r.Loaded);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Frozen_Companys_Receivables_Are_Left_Out_Of_The_Sweep_Batch()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var (order, _) = Seed(ctx, paid: false);
                var owedToFrozen = Receivable.ForCashCancellationFee(order, 300m);
                owedToFrozen.TenantId = TestTenants.Second;
                ctx.Receivables.Add(owedToFrozen);
                await ctx.CommitAsync(CancellationToken.None);

                var company = await ctx.Tenants.SingleAsync(t => t.Id == TestTenants.Second);
                company.RequestWindDown(DateOnly.FromDateTime(DateTime.UtcNow), "admin-frozen", DateTimeOffset.UtcNow.AddDays(-2));
                company.Deactivate("admin-frozen", DateTimeOffset.UtcNow.AddDays(-1));
                company.RequestArchive("admin-frozen", DateTimeOffset.UtcNow);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var batch = await provider.GetRequiredService<IReceivableRepository>()
                    .GetUnchargedOpenIgnoringTenantAsync(50, CancellationToken.None);
                return batch.Select(r => r.Id).ToHashSet();
            },
            assert: (CleansiaDbContext _, HashSet<string> batch) =>
            {
                Assert.Equal(new HashSet<string> { _receivableId }, batch);
                return Task.CompletedTask;
            });
    }

    private (Order Order, Receivable Receivable) Seed(CleansiaDbContext ctx, bool paid = true)
    {
        var language = Language.Create("en", "English");
        language.Id = LanguageId;
        ctx.Languages.Add(language);

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        ctx.Currencies.Add(currency);

        var customer = User.CreateWithPassword(CustomerEmail, "12345678Test!", "Fee", "Owner");
        ctx.Users.Add(customer);

        var order = Order.Create(
            "Fee Owner", CustomerEmail, "+420777000111",
            Address.Create("Poplatkova 1", "Praha", "11000", CountryId),
            rooms: 2, bathrooms: 1, DateTime.UtcNow.AddDays(-1), PaymentType.Card, 1500m, CurrencyId, PaymentStatus.Paid,
            userId: customer.Id);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        ctx.Orders.Add(order);

        var receivable = Receivable.ForCashCancellationFee(order, 375m);
        if (paid)
        {
            receivable.MarkPaid("pi_fee", DateTimeOffset.UtcNow);
        }

        ctx.Receivables.Add(receivable);

        (_orderId, _receivableId, _customerId) = (order.Id, receivable.Id, customer.Id);
        return (order, receivable);
    }
}
