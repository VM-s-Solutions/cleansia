using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0064 D3 — the settlement facts on Postgres, one seeded row per fact for company B beside a
/// settled twin that must not count, and a full mirror under company A that the filter must hide. The
/// wind-down cut-off is midnight of the date in the market's zone: an open order after it counts, one
/// before it does not, and the count is zero while no date is set.
/// </summary>
[Collection("PostgresCollection")]
public sealed class CompanySettlementReaderTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private const string CountryId = "country-sk-settle";
    private const string CurrencyId = "currency-eur-settle";
    private const string LanguageId = "language-en-settle";
    private static readonly DateOnly WindDownFrom = new(2026, 10, 1);
    private static readonly DateTime LatestCardClean = new(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);

    private NpgsqlDataSource _dataSource = default!;
    private readonly MutableTenantProvider _tenantProvider = new();

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            Database = "company_settlement_reader_test"
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
        await DropEveryForeignKeyButTheTenantOnesAsync(conn);
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

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    /// <summary>The facts are counts over books rows; the people and catalogue they point at are not the subject.</summary>
    private static async Task DropEveryForeignKeyButTheTenantOnesAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(
            """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN SELECT conname, conrelid::regclass AS tbl FROM pg_constraint
                       WHERE contype = 'f' AND confrelid <> '"Tenants"'::regclass
              LOOP
                EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname);
              END LOOP;
            END $$;
            """,
            conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SeedAsync()
    {
        _tenantProvider.ClearTenantOverride();
        await using (var catalogue = NewContext())
        {
            var country = Country.Create("Slovakia", "SVK", "SK", isServiced: true);
            country.Id = CountryId;
            catalogue.Countries.Add(country);
            var currency = Currency.Create("EUR", "€", "Euro");
            currency.Id = CurrencyId;
            currency.IsActive = true;
            catalogue.Currencies.Add(currency);
            catalogue.CountryConfigurations.Add(
                CountryConfiguration.Create(CountryId, "EUR", "sk", 0.20m, timeZoneId: "Europe/Bratislava")
                    .AssignOperator(TestTenants.Second));
            var registry = await catalogue.Tenants.SingleAsync(t => t.Id == TestTenants.Second);
            registry.RequestWindDown(WindDownFrom, "admin-b", DateTimeOffset.UtcNow);
            await catalogue.CommitAsync(CancellationToken.None);
        }

        await SeedBooksAsync(TestTenants.Second, "b");
        await SeedBooksAsync(TestTenants.Default, "a");
    }

    private async Task SeedBooksAsync(string tenantId, string key)
    {
        _tenantProvider.SetTenantOverride(tenantId);
        await using var ctx = NewContext();

        // 00:00 on the wind-down date in Bratislava is 22:00 UTC the evening before.
        var openAfterCutoff = OpenOrder($"{key}-open-after", new DateTime(2026, 9, 30, 23, 0, 0, DateTimeKind.Utc));
        var openBeforeCutoff = OpenOrder($"{key}-open-before", new DateTime(2026, 9, 30, 21, 0, 0, DateTimeKind.Utc));
        var completedAwaitingPay = CompletedOrder($"{key}-await-pay", payCalculated: false);
        var completedPaid = CompletedOrder($"{key}-pay-done", payCalculated: true);
        var cardPaidNoReceipt = CardPaidOrder($"{key}-card-no-receipt", LatestCardClean);
        var cardPaidWithReceipt = CardPaidOrder($"{key}-card-receipt", LatestCardClean.AddDays(-10));
        var cashWithReceipt = OpenOrder($"{key}-cash-receipt", new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc));
        cashWithReceipt.AddOrderStatus(Track(OrderStatus.Cancelled, cashWithReceipt, DateTimeOffset.UtcNow));
        var cancelledCashNoReceipt = OpenOrder($"{key}-cash-cancelled", new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc));
        cancelledCashNoReceipt.AddOrderStatus(Track(OrderStatus.Cancelled, cancelledCashNoReceipt, DateTimeOffset.UtcNow));
        ctx.Orders.AddRange(openAfterCutoff, openBeforeCutoff, completedAwaitingPay, completedPaid, cardPaidNoReceipt, cardPaidWithReceipt, cashWithReceipt, cancelledCashNoReceipt);

        var registered = OrderReceipt.Create(cashWithReceipt.Id, $"R-{key}-1", "r1.pdf", "receipts/r1.pdf", LanguageId);
        registered.SetFiscalData("eet", $"FIK-{key}", DateTime.UtcNow);
        var retrying = OrderReceipt.Create(cardPaidWithReceipt.Id, $"R-{key}-2", "r2.pdf", "receipts/r2.pdf", LanguageId);
        retrying.ScheduleImmediateFiscalRetry();
        ctx.OrderReceipts.AddRange(registered, retrying);

        var pending = Refund.Create(cardPaidNoReceipt.Id, $"refund:{key}-card-no-receipt:admin", 10m, "EUR", RefundReason.ServiceNotRendered, RefundSource.AppRefund);
        var succeeded = Refund.Create(cardPaidWithReceipt.Id, $"refund:{key}-card-receipt:admin", 10m, "EUR", RefundReason.ServiceNotRendered, RefundSource.AppRefund);
        succeeded.MarkSucceeded("re_1", DateTimeOffset.UtcNow);
        ctx.Refunds.AddRange(pending, succeeded);

        var activeMembership = UserMembership.Create($"user-{key}-1", "plan-1", CurrencyId, $"sub_{key}_active", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(27));
        var cancelledMembership = UserMembership.Create($"user-{key}-2", "plan-1", CurrencyId, $"sub_{key}_cancelled", DateTime.UtcNow.AddDays(-33), DateTime.UtcNow.AddDays(-3));
        cancelledMembership.UpdateFromStripeWebhook("canceled", cancelledMembership.CurrentPeriodStart, cancelledMembership.CurrentPeriodEnd, null);
        ctx.UserMemberships.AddRange(activeMembership, cancelledMembership);

        var positive = CreditAccount.Create($"user-{key}-1", CurrencyId, "seed");
        positive.Issue(300m, CreditTransactionReason.Goodwill, $"credit:{key}:goodwill", "seed");
        var empty = CreditAccount.Create($"user-{key}-2", CurrencyId, "seed");
        ctx.CreditAccounts.AddRange(positive, empty);

        var active = Template($"user-{key}-1", $"saved-{key}-1");
        var paused = Template($"user-{key}-2", $"saved-{key}-2").Pause();
        ctx.RecurringBookingTemplates.AddRange(active, paused);

        var open = PayPeriod.Create(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15));
        var closed = PayPeriod.Create(new DateOnly(2026, 8, 16), new DateOnly(2026, 8, 31)).Close("admin", "settled");
        ctx.PayPeriods.AddRange(open, closed);

        var approved = EmployeeInvoice.Create($"emp-{key}-1", closed.Id, 1, 100m, CurrencyId, $"1{key.Length}0001", $"INV-{key}-1").Approve("admin");
        var paid = EmployeeInvoice.Create($"emp-{key}-2", closed.Id, 1, 100m, CurrencyId, $"1{key.Length}0002", $"INV-{key}-2").Approve("admin").MarkAsPaid();
        var cancelled = EmployeeInvoice.Create($"emp-{key}-3", closed.Id, 1, 100m, CurrencyId, $"1{key.Length}0003", $"INV-{key}-3").Cancel("void", "admin");
        var inOpenPeriod = EmployeeInvoice.Create($"emp-{key}-4", open.Id, 1, 100m, CurrencyId, $"1{key.Length}0004", $"INV-{key}-4");
        ctx.EmployeeInvoices.AddRange(approved, paid, cancelled, inOpenPeriod);

        var uninvoiced = OrderEmployeePay.Create(completedPaid.Id, $"emp-{key}-1", closed.Id, CurrencyId, 50m, totalPay: 50m);
        var invoiced = OrderEmployeePay.Create(completedPaid.Id, $"emp-{key}-2", closed.Id, CurrencyId, 50m, totalPay: 50m).AssignToInvoice(paid.Id);
        var inOpen = OrderEmployeePay.Create(completedPaid.Id, $"emp-{key}-4", open.Id, CurrencyId, 50m, totalPay: 50m);
        ctx.OrderEmployeePays.AddRange(uninvoiced, invoiced, inOpen);

        var underReview = new Dispute(cardPaidNoReceipt.Id, $"user-{key}-1", DisputeReason.Other, "open", "seed");
        underReview.UpdateStatus(DisputeStatus.UnderReview, "admin");
        var resolved = new Dispute(cardPaidWithReceipt.Id, $"user-{key}-1", DisputeReason.Other, "done", "seed");
        resolved.Resolve("admin", refundAmount: null, "done");
        ctx.Disputes.AddRange(underReview, resolved);

        completedPaid.MarkCashCollected($"emp-{key}-1", DateTime.UtcNow.AddDays(-2), 100m);
        completedAwaitingPay.MarkCashCollected($"emp-{key}-2", DateTime.UtcNow.AddDays(-2), 100m);
        ctx.CashLedgerEntries.AddRange(
            CashLedgerEntry.ForCollection(completedPaid),
            CashLedgerEntry.ForCollection(completedAwaitingPay),
            CashLedgerEntry.ForRemittance($"emp-{key}-2", CurrencyId, 100m, null, DateTime.UtcNow.AddDays(-1)));

        await ctx.CommitAsync(CancellationToken.None);
        _tenantProvider.ClearTenantOverride();
    }

    private static Order OpenOrder(string id, DateTime cleaningAt) => NewOrder(id, cleaningAt, PaymentType.Cash, PaymentStatus.Pending);

    private static Order CompletedOrder(string id, bool payCalculated)
    {
        var order = NewOrder(id, new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc), PaymentType.Cash, PaymentStatus.Paid);
        order.AddOrderStatus(Track(OrderStatus.Completed, order, DateTimeOffset.UtcNow));
        if (payCalculated)
        {
            order.MarkEmployeePayCalculated();
        }

        return order;
    }

    private static Order CardPaidOrder(string id, DateTime cleaningAt)
    {
        var order = NewOrder(id, cleaningAt, PaymentType.Card, PaymentStatus.Paid);
        order.AddOrderStatus(Track(OrderStatus.Completed, order, DateTimeOffset.UtcNow));
        order.MarkEmployeePayCalculated();
        return order;
    }

    private static Order NewOrder(string id, DateTime cleaningAt, PaymentType paymentType, PaymentStatus paymentStatus)
    {
        var order = Order.Create(
            customerName: "Settle Customer",
            customerEmail: $"{id}@cleansia.test",
            customerPhone: "+421900000000",
            customerAddress: Address.Create("Hlavna 1", "Bratislava", "81101", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: cleaningAt,
            paymentType: paymentType,
            totalPrice: 100m,
            currencyId: CurrencyId,
            paymentStatus: paymentStatus,
            userId: $"user-{id}",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = id;
        order.AddOrderStatus(Track(OrderStatus.New, order, DateTimeOffset.UtcNow.AddMinutes(-10)));
        return order;
    }

    private static OrderStatusTrack Track(OrderStatus status, Order order, DateTimeOffset at)
    {
        var track = OrderStatusTrack.Create(status, order);
        track.Created("seed", at);
        return track;
    }

    private static RecurringBookingTemplate Template(string userId, string savedAddressId) =>
        RecurringBookingTemplate.Create(
            userId, RecurrenceFrequency.Weekly, System.DayOfWeek.Monday, new TimeOnly(9, 0), 1, 1, savedAddressId,
            ["service-1"], [], PaymentType.Cash, DateTime.UtcNow.AddDays(-7));

    private CompanySettlementReader ReaderFor(string tenantId, CleansiaDbContext ctx)
    {
        _tenantProvider.SetTenantOverride(tenantId);
        return new CompanySettlementReader(ctx, _tenantProvider);
    }

    [Fact]
    public async Task Every_Fact_Counts_The_Companys_Own_Unsettled_Rows_And_Nothing_Settled()
    {
        await using var ctx = NewContext();

        var facts = await ReaderFor(TestTenants.Second, ctx).ReadAsync(CancellationToken.None);

        Assert.Equal(2, facts.OpenOrders);
        Assert.Equal(1, facts.OpenOrdersOnOrAfterWindDownFrom);
        Assert.Equal(1, facts.ActiveTemplates);
        Assert.Equal(1, facts.ActiveMemberships);
        Assert.Equal(1, facts.CreditBalances);
        Assert.Equal(1, facts.PendingRefunds);
        Assert.Equal(1, facts.OrdersAwaitingPay);
        // The card-paid order without a receipt, the awaiting-pay and pay-done cash orders (owed at
        // creation) and the two open cash orders; the receipted ones are settled, and the cancelled
        // cash order without one owes none — nothing was collected on it.
        Assert.Equal(5, facts.OrdersAwaitingReceipt);
        Assert.Equal(1, facts.ReceiptsAwaitingFiscalRegistration);
        Assert.Equal(1, facts.OpenPayPeriods);
        Assert.Equal(1, facts.UnpaidInvoices);
        Assert.Equal(1, facts.UninvoicedPayRows);
        Assert.Equal(1, facts.OpenDisputes);
        // The cleaner who still holds the cash they collected; the one who handed it back holds none.
        Assert.Equal(1, facts.CleanersHoldingCash);
        Assert.Equal(LatestCardClean, facts.LatestCardPaidCleaningDateTime);
    }

    [Fact]
    public async Task The_Other_Companys_Mirror_Is_Counted_Under_Its_Own_Claim_Only_And_Has_No_WindDown_Cutoff()
    {
        await using var ctx = NewContext();

        var facts = await ReaderFor(TestTenants.Default, ctx).ReadAsync(CancellationToken.None);

        Assert.Equal(2, facts.OpenOrders);
        Assert.Equal(0, facts.OpenOrdersOnOrAfterWindDownFrom);
        Assert.Equal(1, facts.PendingRefunds);
        Assert.Equal(1, facts.OpenDisputes);
        Assert.Equal(LatestCardClean, facts.LatestCardPaidCleaningDateTime);
    }

    [Fact]
    public async Task A_Company_With_No_Books_Reads_All_Zeros_And_No_Card_Clean()
    {
        await using var ctx = NewContext();

        var facts = await ReaderFor("cleansia-nobody", ctx).ReadAsync(CancellationToken.None);

        Assert.Equal(
            new CompanySettlementFacts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null),
            facts);
    }

    /// <summary>
    /// Owner ruling 2026-10-06: an order the cleaner reported unpaid at the door completes with its price
    /// owed and no sale receipt. Once that debt is paid online or written off, nothing more is owed on it,
    /// and no writer could ever issue it a sale receipt, so it must not hold the company's archive back.
    /// </summary>
    [Theory]
    [InlineData(ReceivableStatus.Paid)]
    [InlineData(ReceivableStatus.WrittenOff)]
    public async Task A_Door_Default_Whose_Debt_Was_Paid_Or_Written_Off_Owes_No_Receipt_And_Leaves_The_Company_Archivable(
        ReceivableStatus settled)
    {
        await SeedDoorCompanyAsync(ctx =>
        {
            var (_, debt) = DoorDefault(ctx, "door-settled");
            if (settled == ReceivableStatus.Paid)
            {
                debt.MarkPaid("pi_door_settled", DateTimeOffset.UtcNow);
            }
            else
            {
                debt.WriteOff("manager-door", "Wrong report", DateTimeOffset.UtcNow);
            }
        });
        await using var ctx = NewContext();

        var facts = await ReaderFor(DoorCompany, ctx).ReadAsync(CancellationToken.None);

        Assert.Equal(
            new CompanySettlementFacts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null),
            facts);
    }

    /// <summary>
    /// The price still owed may yet be paid to the cleaner, and cash an administrator recorded is a sale
    /// the company owes a receipt for until it is issued.
    /// </summary>
    [Fact]
    public async Task A_Door_Default_Still_Owed_Or_Recorded_As_Paid_In_Cash_Is_Awaiting_Its_Receipt()
    {
        await SeedDoorCompanyAsync(ctx =>
        {
            DoorDefault(ctx, "door-owed");
            var (recorded, debt) = DoorDefault(ctx, "door-cash-recorded");
            recorded.MarkCashCollected("emp-door", DateTime.UtcNow.AddHours(-1), 100m);
            debt.WriteOff("admin-door", AdminRecordCashReceived.PaidInCashNote, DateTimeOffset.UtcNow);
        });
        await using var ctx = NewContext();

        var facts = await ReaderFor(DoorCompany, ctx).ReadAsync(CancellationToken.None);

        Assert.Equal(2, facts.OrdersAwaitingReceipt);
        Assert.Equal(1, facts.OpenReceivables);
    }

    /// <summary>
    /// Owner ruling 2026-10-06: a fee owed on a cancelled cash booking holds no order open and no receipt
    /// back, yet the freeze would take it out of the booking ban, so the open receivable is a fact of its own.
    /// </summary>
    [Fact]
    public async Task An_Open_Fee_On_A_Cancelled_Booking_Is_An_Open_Receivable_Of_Its_Own_Company_Only()
    {
        await SeedDoorCompanyAsync(ctx =>
        {
            ctx.Receivables.Add(Receivable.ForCashCancellationFee(CancelledCash(ctx, "fee-open"), 50m));
            ctx.Receivables.Add(Receivable.ForLockout(CancelledCash(ctx, "lockout-open"), 100m));
            var paid = Receivable.ForCashCancellationFee(CancelledCash(ctx, "fee-paid"), 50m);
            paid.MarkPaid("pi_fee_paid", DateTimeOffset.UtcNow);
            var writtenOff = Receivable.ForLockout(CancelledCash(ctx, "lockout-written-off"), 100m);
            writtenOff.WriteOff("manager-door", "Goodwill", DateTimeOffset.UtcNow);
            ctx.Receivables.AddRange(paid, writtenOff);
        });
        await using var ctx = NewContext();

        var facts = await ReaderFor(DoorCompany, ctx).ReadAsync(CancellationToken.None);
        var otherCompany = await ReaderFor(TestTenants.Second, ctx).ReadAsync(CancellationToken.None);

        Assert.Equal(2, facts.OpenReceivables);
        Assert.Equal(0, facts.OpenOrders);
        Assert.Equal(0, facts.OrdersAwaitingReceipt);
        Assert.Equal(0, otherCompany.OpenReceivables);
    }

    private static Order CancelledCash(CleansiaDbContext ctx, string orderId)
    {
        var order = NewOrder(orderId, new DateTime(2026, 9, 14, 9, 0, 0, DateTimeKind.Utc), PaymentType.Cash, PaymentStatus.Pending);
        order.AddOrderStatus(Track(OrderStatus.Cancelled, order, DateTimeOffset.UtcNow));
        ctx.Orders.Add(order);
        return order;
    }

    private const string DoorCompany = "cleansia-door-settle";

    private async Task SeedDoorCompanyAsync(Action<CleansiaDbContext> seedBooks)
    {
        _tenantProvider.ClearTenantOverride();
        await using (var registry = NewContext())
        {
            registry.Tenants.Add(Tenant.Create(DoorCompany, "Cleansia Door s.r.o."));
            await registry.CommitAsync(CancellationToken.None);
        }

        _tenantProvider.SetTenantOverride(DoorCompany);
        await using (var books = NewContext())
        {
            seedBooks(books);
            await books.CommitAsync(CancellationToken.None);
        }

        _tenantProvider.ClearTenantOverride();
    }

    /// <summary>What a door report leaves once the crew's pay is calculated: the cash order completed unpaid, and the price owed.</summary>
    private static (Order Order, Receivable Debt) DoorDefault(CleansiaDbContext ctx, string orderId)
    {
        var order = NewOrder(orderId, new DateTime(2026, 9, 12, 9, 0, 0, DateTimeKind.Utc), PaymentType.Cash, PaymentStatus.Pending);
        order.AddOrderStatus(Track(OrderStatus.InProgress, order, DateTimeOffset.UtcNow.AddMinutes(-5)));
        order.AddOrderStatus(Track(OrderStatus.Completed, order, DateTimeOffset.UtcNow));
        order.MarkEmployeePayCalculated();
        ctx.Orders.Add(order);

        var debt = Receivable.ForUnpaidCash(order);
        ctx.Receivables.Add(debt);
        return (order, debt);
    }

    private sealed class MutableTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
