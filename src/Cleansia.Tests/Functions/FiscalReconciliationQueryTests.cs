using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0002 D3.4 + ADR-0004 C-B — the two NEW read queries that feed the
/// dispatch reconciliation sweep, exercised against a REAL <see cref="CleansiaDbContext"/> over SQLite
/// (so the model + global tenant query filter materialize; the cross-tenant read uses
/// <c>IgnoreQueryFilters</c> like <c>GetDueForRetryAsync</c>). No Postgres/Docker.
///
/// <para><b>Receipt predicate (C-B):</b> <c>Paid</c>/<c>Cash</c>-eligible orders OLDER than the
/// threshold AND (<c>Receipt is null</c> OR <c>Receipt.FiscalCode == null</c>) — the C-B widening
/// beyond the original D3.4 "no Receipt" to also catch the claimed-but-unregistered rows.
/// An order WITHIN the threshold (recently committed) is NOT swept.</para>
///
/// <para><b>Invoice predicate:</b> a <c>PayPeriod</c> with an employee (an <c>OrderEmployeePay</c>
/// row) who has NO <c>EmployeeInvoice</c> for <c>(PayPeriodId, EmployeeId)</c>, older than the
/// threshold.</para>
///
/// <para>Written TEST-FIRST (RED until the two queries exist).</para>
/// </summary>
public sealed class FiscalReconciliationQueryTests : IDisposable
{
    private const string LanguageId = "lang-cz";
    private readonly SqliteConnection _connection;

    public FiscalReconciliationQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // These suites exercise ONLY the recon predicates, not referential integrity, so disable FK
        // enforcement to seed Order/Receipt/PayPeriod/OrderEmployeePay rows without their full graphs.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
        if (!await ctx.Set<Language>().AnyAsync(l => l.Id == LanguageId))
        {
            var language = Language.Create("cz", "Czech");
            language.Id = LanguageId;
            ctx.Add(language);
            await ctx.CommitAsync(CancellationToken.None);
        }
    }

    private static Order NewOrder(
        string orderId,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        DateTimeOffset createdOn)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: paymentStatus);
        order.Id = orderId;
        // The "older than N min" filter keys on the commit time (CreatedOn).
        order.Created("system", createdOn);
        return order;
    }

    private static OrderReceipt NewReceipt(string orderId, string receiptNumber)
        => OrderReceipt.Create(orderId, receiptNumber, $"{receiptNumber}.pdf", $"2026/{orderId}/{receiptNumber}.pdf", LanguageId);

    // ── A STALE Paid order with NO receipt is a receipt-recon candidate ──

    [Fact]
    public async Task Receipt_Recon_Returns_Stale_Paid_Order_With_No_Receipt()
    {
        await EnsureSchemaAsync();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-60);

        await using (var seed = NewContext())
        {
            seed.Add(NewOrder("01HZX9N6M7Q8R9S0T1V2W3X410", PaymentType.Card, PaymentStatus.Paid, stale));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new OrderRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetReceiptReconciliationCandidatesAsync(cutoff, take: 50, CancellationToken.None);

        Assert.Single(due);
        Assert.Equal("01HZX9N6M7Q8R9S0T1V2W3X410", due[0].Id);
    }

    // ── An order WITHIN the threshold (recently committed) is NOT swept ──

    [Fact]
    public async Task Receipt_Recon_Skips_Fresh_Order_Within_Threshold()
    {
        await EnsureSchemaAsync();
        var fresh = DateTimeOffset.UtcNow.AddMinutes(-2);

        await using (var seed = NewContext())
        {
            seed.Add(NewOrder("01HZX9N6M7Q8R9S0T1V2W3X411", PaymentType.Card, PaymentStatus.Paid, fresh));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new OrderRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetReceiptReconciliationCandidatesAsync(cutoff, take: 50, CancellationToken.None);

        Assert.Empty(due);
    }

    // ── ADR-0004 C-B — a stale order that HAS a receipt but FiscalCode == null IS swept (the widening) ──

    [Fact]
    public async Task Receipt_Recon_Returns_Stale_Order_With_Receipt_But_Null_FiscalCode()
    {
        await EnsureSchemaAsync();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-60);
        var orderId = "01HZX9N6M7Q8R9S0T1V2W3X412";

        await using (var seed = NewContext())
        {
            var order = NewOrder(orderId, PaymentType.Card, PaymentStatus.Paid, stale);
            var receipt = NewReceipt(orderId, "2026-000412"); // FiscalCode stays null (claimed-but-unregistered)
            seed.Add(order);
            seed.Add(receipt);
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new OrderRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetReceiptReconciliationCandidatesAsync(cutoff, take: 50, CancellationToken.None);

        Assert.Single(due);
        Assert.Equal(orderId, due[0].Id);
    }

    // ── C-B inverse — a stale order whose receipt HAS a FiscalCode is fully realized → NOT swept ──

    [Fact]
    public async Task Receipt_Recon_Skips_Stale_Order_With_Registered_Receipt()
    {
        await EnsureSchemaAsync();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-60);
        var orderId = "01HZX9N6M7Q8R9S0T1V2W3X413";

        await using (var seed = NewContext())
        {
            var order = NewOrder(orderId, PaymentType.Card, PaymentStatus.Paid, stale);
            var receipt = NewReceipt(orderId, "2026-000413");
            receipt.SetFiscalData("cz-eet2", "FIK-OK", DateTime.UtcNow); // fully registered
            seed.Add(order);
            seed.Add(receipt);
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new OrderRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetReceiptReconciliationCandidatesAsync(cutoff, take: 50, CancellationToken.None);

        Assert.Empty(due);
    }

    // ── Eligibility — a stale order that is NEITHER Cash NOR Paid (e.g. Card+Pending) is NOT swept ──

    [Fact]
    public async Task Receipt_Recon_Skips_Ineligible_Card_Pending_Order()
    {
        await EnsureSchemaAsync();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-60);

        await using (var seed = NewContext())
        {
            seed.Add(NewOrder("01HZX9N6M7Q8R9S0T1V2W3X414", PaymentType.Card, PaymentStatus.Pending, stale));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new OrderRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetReceiptReconciliationCandidatesAsync(cutoff, take: 50, CancellationToken.None);

        Assert.Empty(due);
    }

    // ── OR-shape regression — the sweep is now a UNION of a Cash arm and a Paid arm; an order that
    // is BOTH Cash and Paid must appear exactly once ──

    [Fact]
    public async Task Receipt_Recon_Returns_DualEligible_Cash_And_Paid_Order_Once()
    {
        await EnsureSchemaAsync();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-60);

        await using (var seed = NewContext())
        {
            seed.Add(NewOrder("01HZX9N6M7Q8R9S0T1V2W3X415", PaymentType.Cash, PaymentStatus.Paid, stale));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new OrderRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetReceiptReconciliationCandidatesAsync(cutoff, take: 50, CancellationToken.None);

        Assert.Single(due);
        Assert.Equal("01HZX9N6M7Q8R9S0T1V2W3X415", due[0].Id);
    }

    // ── OR-shape regression — `take` still selects the GLOBALLY oldest candidates across both
    // arms (oldest-first), not per-arm quotas ──

    [Fact]
    public async Task Receipt_Recon_Take_Selects_Globally_Oldest_Across_Both_Arms()
    {
        await EnsureSchemaAsync();
        var oldest = DateTimeOffset.UtcNow.AddMinutes(-90);
        var middle = DateTimeOffset.UtcNow.AddMinutes(-60);
        var newest = DateTimeOffset.UtcNow.AddMinutes(-30);

        await using (var seed = NewContext())
        {
            seed.Add(NewOrder("01HZX9N6M7Q8R9S0T1V2W3X416", PaymentType.Cash, PaymentStatus.Pending, oldest));
            seed.Add(NewOrder("01HZX9N6M7Q8R9S0T1V2W3X417", PaymentType.Card, PaymentStatus.Paid, middle));
            seed.Add(NewOrder("01HZX9N6M7Q8R9S0T1V2W3X418", PaymentType.Cash, PaymentStatus.Pending, newest));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new OrderRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetReceiptReconciliationCandidatesAsync(cutoff, take: 2, CancellationToken.None);

        Assert.Equal(2, due.Count);
        Assert.Equal("01HZX9N6M7Q8R9S0T1V2W3X416", due[0].Id);
        Assert.Equal("01HZX9N6M7Q8R9S0T1V2W3X417", due[1].Id);
    }

    // ── A stale PayPeriod with an employee lacking an EmployeeInvoice is swept ──

    [Fact]
    public async Task Invoice_Recon_Returns_PayPeriod_Employee_Missing_Invoice()
    {
        await EnsureSchemaAsync();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-60);

        var payPeriodId = "01HZX9N6M7Q8R9S0T1V2W3XP01";
        var employeeId = "01HZX9N6M7Q8R9S0T1V2W3XE01";

        await using (var seed = NewContext())
        {
            var period = PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)));
            period.Id = payPeriodId;
            period.Created("system", stale);
            seed.Add(period);

            var pay = OrderEmployeePay.Create(
                orderId: "01HZX9N6M7Q8R9S0T1V2W3XO01",
                employeeId: employeeId,
                payPeriodId: payPeriodId,
                basePay: 500m,
                totalPay: 500m);
            seed.Add(pay);
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new PayPeriodRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetInvoiceReconciliationCandidatesAsync(cutoff, take: 50, CancellationToken.None);

        Assert.Single(due);
        Assert.Equal(payPeriodId, due[0].PayPeriodId);
        Assert.Equal(employeeId, due[0].EmployeeId);
    }

    // ── Invoice inverse — an employee who already HAS an invoice for the period is NOT swept ──

    [Fact]
    public async Task Invoice_Recon_Skips_Employee_With_Existing_Invoice()
    {
        await EnsureSchemaAsync();
        var stale = DateTimeOffset.UtcNow.AddMinutes(-60);

        var payPeriodId = "01HZX9N6M7Q8R9S0T1V2W3XP02";
        var employeeId = "01HZX9N6M7Q8R9S0T1V2W3XE02";

        await using (var seed = NewContext())
        {
            var period = PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)));
            period.Id = payPeriodId;
            period.Created("system", stale);
            seed.Add(period);

            seed.Add(OrderEmployeePay.Create(
                orderId: "01HZX9N6M7Q8R9S0T1V2W3XO02",
                employeeId: employeeId,
                payPeriodId: payPeriodId,
                basePay: 500m,
                totalPay: 500m));

            var invoice = EmployeeInvoice.Create(employeeId, payPeriodId, totalOrders: 1, subTotal: 500m, currencyId: "czk");
            seed.Add(invoice);
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new PayPeriodRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetInvoiceReconciliationCandidatesAsync(cutoff, take: 50, CancellationToken.None);

        Assert.Empty(due);
    }

    // ── Invoice freshness — a PayPeriod within the threshold is NOT swept ──

    [Fact]
    public async Task Invoice_Recon_Skips_Fresh_PayPeriod_Within_Threshold()
    {
        await EnsureSchemaAsync();
        var fresh = DateTimeOffset.UtcNow.AddMinutes(-2);

        var payPeriodId = "01HZX9N6M7Q8R9S0T1V2W3XP03";
        var employeeId = "01HZX9N6M7Q8R9S0T1V2W3XE03";

        await using (var seed = NewContext())
        {
            var period = PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)));
            period.Id = payPeriodId;
            period.Created("system", fresh);
            seed.Add(period);

            seed.Add(OrderEmployeePay.Create(
                orderId: "01HZX9N6M7Q8R9S0T1V2W3XO03",
                employeeId: employeeId,
                payPeriodId: payPeriodId,
                basePay: 500m,
                totalPay: 500m));
            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repo = new PayPeriodRepository(ctx);
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        var due = await repo.GetInvoiceReconciliationCandidatesAsync(cutoff, take: 50, CancellationToken.None);

        Assert.Empty(due);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
