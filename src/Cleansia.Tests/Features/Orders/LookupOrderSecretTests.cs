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
/// The anonymous single <see cref="LookupOrder.Handler"/> gates on ONE thing: the per-order access
/// token the booking's confirmation e-mail carried.
///
/// <para>It used to gate on (display order number, e-mail, confirmation code). None of those three is
/// a secret the platform keeps: the number is sequential, the e-mail is not private, and the code was
/// served on the order detail to every cleaner assigned to the job — so a cleaner held the whole key
/// to their own customer's booking, including the cancellation that charges the customer the
/// 25 % / 50 % tier.</para>
///
/// These cases pin the properties that matter:
/// <list type="bullet">
///   <item>the live token returns the order;</item>
///   <item>a token nobody issued returns NOTHING, the same way an expired or revoked one does, so the
///     endpoint is never an oracle for which bookings exist;</item>
///   <item>an account booking is never reachable by a guest token, whatever the token says;</item>
///   <item>only the hash is ever compared — the raw value appears in no persisted row.</item>
/// </list>
/// </summary>
public class LookupOrderSecretTests
{
    private const string OrderNumber = "CLS-2026-0001";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IGuestOrderAccessTokenRepository> _tokenRepository = new();
    private readonly List<GuestOrderAccessToken> _tokens = [];

    private static Order BuildOrder(string? userId = null)
    {
        var address = Address.Create("Street 1", "Praha", "14000", "country-1");
        var currency = Core.Domain.Internationalization.Currency.Create("CZK", "Kč", "Czech Koruna");

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
            paymentStatus: PaymentStatus.Pending,
            userId: userId);

        order.Id = "ord-1";
        order.SetCurrency(currency);
        order.UpdateEstimatedTime(120);
        // MapToDetail dereferences the latest status row.
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));

        // The display number is generated, so it is forced to a known value here rather than read
        // back — a test that asserts against whatever the entity happened to generate proves nothing.
        typeof(Order).GetProperty(nameof(Order.DisplayOrderNumber))!.SetValue(order, OrderNumber);

        return order;
    }

    private void SeedOrder(Order order)
    {
        _orderRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        _tokenRepository.Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(_tokens.AsQueryable().BuildMock());
    }

    private string IssueToken(string orderId, DateTimeOffset? expiresOn = null, bool revoked = false)
    {
        var token = GuestOrderAccessToken.Issue(orderId, expiresOn ?? DateTimeOffset.UtcNow.AddDays(30));
        if (revoked)
        {
            token.Revoke(DateTimeOffset.UtcNow);
        }

        _tokens.Add(token);
        return token.RawToken!;
    }

    private LookupOrder.Handler CreateHandler() =>
        new(new GuestOrderAccess(_orderRepository.Object, _tokenRepository.Object));

    [Fact]
    public async Task The_Live_Token_Returns_The_Order()
    {
        SeedOrder(BuildOrder());
        var token = IssueToken("ord-1");

        var result = await CreateHandler().Handle(new LookupOrder.Query(token), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderNumber, result.Value!.DisplayOrderNumber);
    }

    [Fact]
    public async Task An_Unissued_Token_And_A_Dead_Token_Fail_Identically()
    {
        // The endpoint must not tell a caller that a booking EXISTS but their link has lapsed — that
        // turns it into an oracle. Every refusal is the same refusal.
        SeedOrder(BuildOrder());
        var expired = IssueToken("ord-1", expiresOn: DateTimeOffset.UtcNow.AddMinutes(-1));
        var revoked = IssueToken("ord-1", revoked: true);
        var handler = CreateHandler();

        var unissued = await handler.Handle(
            new LookupOrder.Query(SecurityTokens.Generate(SecurityTokens.DurableTokenByteLength)),
            CancellationToken.None);
        var lapsed = await handler.Handle(new LookupOrder.Query(expired), CancellationToken.None);
        var withdrawn = await handler.Handle(new LookupOrder.Query(revoked), CancellationToken.None);

        foreach (var refusal in new[] { unissued, lapsed, withdrawn })
        {
            Assert.False(refusal.IsSuccess);
            Assert.Equal(unissued.Error?.Code, refusal.Error?.Code);
            Assert.Equal(unissued.Error?.Message, refusal.Error?.Message);
        }
    }

    [Fact]
    public async Task An_Account_Booking_Is_Not_Reachable_By_A_Guest_Token()
    {
        // A token can only ever be issued for a guest booking, but the guest read refuses an owned
        // order in its own right rather than trusting that — an account holder signs in.
        SeedOrder(BuildOrder(userId: "user-1"));
        var token = IssueToken("ord-1");

        var result = await CreateHandler().Handle(new LookupOrder.Query(token), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Only_The_Hash_Is_Persisted()
    {
        SeedOrder(BuildOrder());
        var raw = IssueToken("ord-1");

        var stored = Assert.Single(_tokens);
        Assert.NotEqual(raw, stored.TokenHash);
        Assert.Equal(SecurityTokens.Hash(raw), stored.TokenHash);
    }
}
