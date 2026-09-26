using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The anonymous <see cref="LookupOrderBatch.Handler"/> is the only gate protecting an order's data
/// from an unauthenticated caller (no tenant claim, S3 — the global tenant filter is bypassed). The
/// per-item secret is the order's own access token, the 256-bit value its confirmation e-mail carried.
///
/// <para>It used to be the (OrderId, Email) pair, and before that the order's confirmation code, which
/// every assigned cleaner was served on the order detail. These cases pin what replaced it:</para>
/// <list type="bullet">
///   <item>a batch over the cap (&gt; 10 items) returns nothing — no bulk enumeration;</item>
///   <item>only orders whose live token was presented come back;</item>
///   <item>an expired or revoked token resolves to nothing, and its valid siblings still resolve;</item>
///   <item>a null / empty token is dropped without reaching the repository predicate.</item>
/// </list>
/// </summary>
public class LookupOrderBatchSecretTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IGuestOrderAccessTokenRepository> _tokenRepository = new();
    private readonly List<GuestOrderAccessToken> _tokens = [];

    private static Order BuildOrder(string id)
    {
        var address = Address.Create("Street 1", "Praha", "14000", "country-1");
        var currency = Cleansia.Core.Domain.Internationalization.Currency.Create("CZK", "Kč", "Czech Koruna");

        var order = Order.Create(
            customerName: "Alice",
            customerEmail: "alice@example.com",
            customerPhone: "+420123456789",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
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

    private void SeedOrders(params Order[] orders)
    {
        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant()).Returns(orders.AsQueryable().BuildMock());
        _tokenRepository.Setup(r => r.GetQueryableIgnoringTenant()).Returns(_tokens.AsQueryable().BuildMock());
    }

    private string IssueToken(string orderId, DateTimeOffset? expiresOn = null, bool revoked = false)
    {
        var token = GuestOrderAccessToken.Issue(
            orderId, expiresOn ?? DateTimeOffset.UtcNow.AddDays(30));
        if (revoked)
        {
            token.Revoke(DateTimeOffset.UtcNow);
        }

        _tokens.Add(token);
        return token.RawToken!;
    }

    private LookupOrderBatch.Handler CreateHandler() =>
        new(new GuestOrderAccess(_orderRepository.Object, _tokenRepository.Object));

    [Fact]
    public async Task Over_Cap_More_Than_Ten_Items_Returns_Nothing()
    {
        SeedOrders(BuildOrder("ord-1"));
        var tokens = Enumerable.Range(1, LookupOrderBatch.MaxItems + 1)
            .Select(i => IssueToken($"ord-{i}"))
            .ToArray();

        var result = await CreateHandler().Handle(new LookupOrderBatch.Query(tokens), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Orders);
        _orderRepository.Verify(r => r.GetQueryableIgnoringTenant(), Times.Never);
    }

    [Fact]
    public async Task A_Live_Token_Returns_Its_Own_Order_And_Only_That_One()
    {
        SeedOrders(BuildOrder("ord-1"), BuildOrder("ord-2"));
        var token = IssueToken("ord-1");
        IssueToken("ord-2");

        var result = await CreateHandler().Handle(new LookupOrderBatch.Query([token]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value!.Orders);
        Assert.Equal("ord-1", only.Id);
    }

    [Fact]
    public async Task An_Unknown_Token_Yields_Nothing()
    {
        SeedOrders(BuildOrder("ord-1"));
        IssueToken("ord-1");

        var result = await CreateHandler().Handle(
            new LookupOrderBatch.Query([SecurityTokens.Generate(SecurityTokens.DurableTokenByteLength)]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Orders);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_Dead_Token_Is_Dropped_And_Its_Live_Sibling_Still_Resolves(bool expired, bool revoked)
    {
        SeedOrders(BuildOrder("ord-1"), BuildOrder("ord-2"));
        var dead = IssueToken("ord-1",
            expiresOn: expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : null,
            revoked: revoked);
        var live = IssueToken("ord-2");

        var result = await CreateHandler().Handle(
            new LookupOrderBatch.Query([dead, live]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value!.Orders);
        Assert.Equal("ord-2", only.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_Blank_Token_Is_Dropped_Without_Reaching_The_Lookup(string? blank)
    {
        SeedOrders(BuildOrder("ord-1"));
        IssueToken("ord-1");

        var result = await CreateHandler().Handle(
            new LookupOrderBatch.Query([blank!]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Orders);
        _orderRepository.Verify(r => r.GetQueryableIgnoringTenant(), Times.Never);
    }
}
