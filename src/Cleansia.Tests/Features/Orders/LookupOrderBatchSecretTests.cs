using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The anonymous <see cref="LookupOrderBatch.Handler"/> is the only gate
/// protecting an order's data from an unauthenticated caller (no tenant claim, S3 — the global tenant
/// filter is bypassed). The per-item shared secret is the (OrderId, Email) pair: the GUID
/// <c>OrderId</c> is itself a secret the guest only obtains by first proving the
/// (DisplayOrderNumber, Email) pair through single <see cref="LookupOrder"/> — the batch matches the
/// SAME pairing as single (email is the gate in both; the GUID is an equal-or-stronger secret than the
/// human-typed DisplayOrderNumber), so the batch does NOT widen the secret. These cases pin:
///   - a batch over the cap (&gt; 10 items) returns nothing (no bulk enumeration);
///   - only rows whose (OrderId, Email) matches the supplied per-item secret come back;
///   - a null / empty Email item is DROPPED before <c>.ToLower()</c> — no NRE — and the
///     valid items in the same batch still resolve;
///   - an item with a wrong / absent email for a real order yields NOTHING for that item (the
///     email-match is the only gate; no anonymous tenant leak).
/// Written red -> green per knowledge/testing.md (the null-email drop predates the handler fix).
/// </summary>
public class LookupOrderBatchSecretTests
{
    private const string MatchingEmail = "alice@example.com";
    private const string WrongEmail = "attacker@evil.com";

    private readonly Mock<IOrderRepository> _orderRepository = new();

    private static Order BuildOrder(string id, string customerEmail)
    {
        var address = Address.Create("Street 1", "Praha", "14000", "country-1");
        var currency = Cleansia.Core.Domain.Internationalization.Currency.Create("CZK", "Kč", "Czech Koruna", 1m);

        var order = Order.Create(
            customerName: "Alice",
            customerEmail: customerEmail,
            customerPhone: "+420123456789",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Pending);
        order.Id = id;
        order.SetCurrency(currency);
        order.UpdateEstimatedTime(120);
        // GetCurrentOrderStatus() inside MapToDetail dereferences the latest status row,
        // so every mappable order needs at least one status history entry.
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    private void SeedOrders(params Order[] orders) =>
        _orderRepository.Setup(r => r.GetQueryable()).Returns(orders.AsQueryable().BuildMock());

    private LookupOrderBatch.Handler CreateHandler() => new(_orderRepository.Object);

    private static LookupOrderBatch.Query Batch(params LookupOrderBatch.OrderLookupItem[] items) =>
        new(items);

    [Fact]
    public async Task Over_Cap_More_Than_Ten_Items_Returns_Nothing()
    {
        // a 11-item batch is rejected outright — the handler never enumerates the repo.
        SeedOrders(BuildOrder("ord-1", MatchingEmail));
        var items = Enumerable.Range(1, 11)
            .Select(i => new LookupOrderBatch.OrderLookupItem($"ord-{i}", MatchingEmail))
            .ToArray();

        var result = await CreateHandler().Handle(Batch(items), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Orders);
        _orderRepository.Verify(r => r.GetQueryable(), Times.Never);
    }

    [Fact]
    public async Task Matching_OrderId_And_Email_Returns_The_Row()
    {
        // the (OrderId, Email) secret pair matches → the row comes back.
        SeedOrders(BuildOrder("ord-1", MatchingEmail));

        var result = await CreateHandler().Handle(
            Batch(new LookupOrderBatch.OrderLookupItem("ord-1", MatchingEmail)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value!.Orders);
        Assert.Equal("ord-1", only.Id);
    }

    [Fact]
    public async Task Email_Match_Is_Case_Insensitive_Same_As_Single_Lookup()
    {
        // Secret-pair consistency with single LookupOrder, which lower-cases both sides
        // (LookupOrder.cs:53). The batch must apply the SAME case-insensitive email gate.
        SeedOrders(BuildOrder("ord-1", "Alice@Example.com"));

        var result = await CreateHandler().Handle(
            Batch(new LookupOrderBatch.OrderLookupItem("ord-1", "ALICE@example.COM")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Orders);
    }

    [Fact]
    public async Task Wrong_Email_For_Real_Order_Yields_Nothing_No_Tenant_Leak()
    {
        // the order exists, the GUID is correct, but the email does not match → NOTHING.
        // The email-match is the only gate; an anonymous caller who cannot prove the email gets no data.
        SeedOrders(BuildOrder("ord-1", MatchingEmail));

        var result = await CreateHandler().Handle(
            Batch(new LookupOrderBatch.OrderLookupItem("ord-1", WrongEmail)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Orders);
    }

    [Fact]
    public async Task Null_Email_Item_Is_Dropped_No_NullReference()
    {
        // a null-Email item must be dropped BEFORE i.Email.ToLower() — no NRE.
        // Today line :43 dereferences i.Email unconditionally and throws.
        SeedOrders(BuildOrder("ord-1", MatchingEmail));

        var result = await CreateHandler().Handle(
            Batch(new LookupOrderBatch.OrderLookupItem("ord-1", null!)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Orders);
    }

    [Fact]
    public async Task Empty_Email_Item_Is_Dropped_Valid_Sibling_Still_Resolves()
    {
        // an empty-Email item is dropped; a valid item in the SAME batch still resolves.
        SeedOrders(
            BuildOrder("ord-1", MatchingEmail),
            BuildOrder("ord-2", "bob@example.com"));

        var result = await CreateHandler().Handle(
            Batch(
                new LookupOrderBatch.OrderLookupItem("ord-1", ""),
                new LookupOrderBatch.OrderLookupItem("ord-2", "bob@example.com")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value!.Orders);
        Assert.Equal("ord-2", only.Id);
    }
}
