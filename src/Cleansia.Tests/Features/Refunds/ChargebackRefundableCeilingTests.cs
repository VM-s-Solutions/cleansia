using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// A bank chargeback (ADR-0006) lands as a <see cref="RefundSource.Chargeback"/> reconciliation row —
/// funds the bank already pulled. ADR-0009 makes it window-EXEMPT (it is not an app-issued refund) yet
/// it MUST count toward <c>refundable(order) = amountCharged − Σ(succeeded refunds)</c> so an admin cannot
/// issue a second refund on a charged-back order. This spins a real <see cref="CleansiaDbContext"/> over
/// SQLite so the consumed-ceiling read runs against the actual EF query: the chargeback row reduces the
/// refundable amount exactly like an app refund, and the consumed read carries no time/window predicate.
/// </summary>
public sealed class ChargebackRefundableCeilingTests : IDisposable
{
    private const string OrderId = "order-cb-1";

    private readonly SqliteConnection _connection;

    public ChargebackRefundableCeilingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private static IUserSessionProvider NewSession() =>
        new TestUserSessionProvider("admin-1", "admin@cleansia.test");

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(options, NewSession(), new NullTenantProvider());
    }

    private async Task SeedOrderWithChargebackAsync(decimal totalPrice, decimal chargebackAmount)
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        currency.Id = "cur-1";
        var country = Country.Create("Czechia", "CZE");
        country.Id = "country-1";
        var address = Address.Create("Main Street 1", "Prague", "11000", country.Id);
        address.Id = "addr-1";
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: null);
        order.Id = OrderId;

        var chargeback = Refund.Create(
            orderId: OrderId,
            refundKey: $"refund:{OrderId}:chargeback",
            amount: chargebackAmount,
            currency: currency.Code,
            reason: RefundReason.AdminDiscretion,
            source: RefundSource.Chargeback);
        chargeback.MarkSucceeded(stripeRefundId: "dp_test", confirmedOnUtc: DateTimeOffset.UtcNow);

        ctx.Add(currency);
        ctx.Add(country);
        ctx.Add(address);
        await ctx.CommitAsync(CancellationToken.None);

        ctx.Add(order);
        ctx.Add(chargeback);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task SeedOrderWithAppRefundAndChargebackAsync(decimal appRefundAmount, decimal chargebackAmount)
    {
        await SeedOrderWithChargebackAsync(totalPrice: 1000m, chargebackAmount: chargebackAmount);

        await using var ctx = NewContext();
        var appRefund = Refund.Create(
            orderId: OrderId,
            refundKey: $"refund:{OrderId}:cancel",
            amount: appRefundAmount,
            currency: "CZK",
            reason: RefundReason.CustomerCancellation,
            source: RefundSource.AppRefund);
        appRefund.MarkSucceeded(stripeRefundId: "re_test", confirmedOnUtc: DateTimeOffset.UtcNow);
        ctx.Add(appRefund);
        await ctx.CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SucceededTotal_IsSourceAgnostic_SummingBothAppRefundAndChargeback()
    {
        await SeedOrderWithAppRefundAndChargebackAsync(appRefundAmount: 300m, chargebackAmount: 400m);

        await using var ctx = NewContext();
        var consumed = await new RefundRepository(ctx)
            .GetSucceededRefundTotalForOrderAsync(OrderId, CancellationToken.None);

        Assert.Equal(700m, consumed);
    }

    [Fact]
    public async Task ChargebackRow_CountsTowardSucceededRefundTotal_ReducingRefundable()
    {
        await SeedOrderWithChargebackAsync(totalPrice: 1000m, chargebackAmount: 1000m);

        await using var ctx = NewContext();
        var consumed = await new RefundRepository(ctx)
            .GetSucceededRefundTotalForOrderAsync(OrderId, CancellationToken.None);

        Assert.Equal(1000m, consumed);
    }

    [Fact]
    public async Task PartialChargeback_LeavesOnlyTheRemainderRefundable()
    {
        await SeedOrderWithChargebackAsync(totalPrice: 1000m, chargebackAmount: 600m);

        await using var ctx = NewContext();
        var consumed = await new RefundRepository(ctx)
            .GetSucceededRefundTotalForOrderAsync(OrderId, CancellationToken.None);

        Assert.Equal(600m, consumed);
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
