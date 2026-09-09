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
/// The anonymous single <see cref="LookupOrder.Handler"/> now gates on THREE
/// things: the display order number, the customer's e-mail, and the order's own
/// confirmation code.
///
/// The code is what makes the gate a gate. A display order number is sequential
/// and an e-mail address is not a secret, so the previous two-factor pair was
/// guessable by anyone who knew a customer's address and could count — and the
/// endpoint is anonymous, so there is no tenant filter behind it (S3).
///
/// These cases pin the properties that matter:
///   - all three correct returns the order;
///   - a wrong code returns NOTHING, and returns it the same way a wrong order
///     number does, so the endpoint is never an oracle for which orders exist;
///   - the code is matched case-insensitively, because it is generated
///     uppercase and then read off an e-mail and typed by hand;
///   - e-mail remains case-insensitive and the order number remains exact.
/// </summary>
public class LookupOrderSecretTests
{
    private const string OrderNumber = "CLS-2026-0001";
    private const string MatchingEmail = "alice@example.com";
    private const string RealCode = "A1B2C3";

    private readonly Mock<IOrderRepository> _orderRepository = new();

    private static Order BuildOrder()
    {
        var address = Address.Create("Street 1", "Praha", "14000", "country-1");
        var currency = Core.Domain.Internationalization.Currency.Create("CZK", "Kč", "Czech Koruna", 1m);

        var order = Order.Create(
            customerName: "Alice",
            customerEmail: MatchingEmail,
            customerPhone: "+420123456789",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Pending);

        order.Id = "ord-1";
        order.SetCurrency(currency);
        order.UpdateEstimatedTime(120);
        // MapToDetail dereferences the latest status row.
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));

        // The code and the display number are generated, so they are forced to
        // known values here rather than read back — a test that asserts against
        // whatever the entity happened to generate proves nothing.
        typeof(Order).GetProperty(nameof(Order.ConfirmationCode))!
            .SetValue(order, RealCode);
        typeof(Order).GetProperty(nameof(Order.DisplayOrderNumber))!
            .SetValue(order, OrderNumber);

        return order;
    }

    private void SeedOrder(Order order) =>
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

    private LookupOrder.Handler CreateHandler() => new(_orderRepository.Object);

    [Fact]
    public async Task All_Three_Correct_Returns_The_Order()
    {
        SeedOrder(BuildOrder());

        var result = await CreateHandler().Handle(
            new LookupOrder.Query(OrderNumber, MatchingEmail, RealCode), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderNumber, result.Value!.DisplayOrderNumber);
    }

    [Fact]
    public async Task Wrong_Confirmation_Code_Returns_Nothing()
    {
        // The number and the e-mail are both right; only the code is wrong.
        // Before the code was part of the predicate this call succeeded.
        SeedOrder(BuildOrder());

        var result = await CreateHandler().Handle(
            new LookupOrder.Query(OrderNumber, MatchingEmail, "ZZZZZZ"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Wrong_Code_And_Unknown_Order_Fail_Identically()
    {
        // The endpoint must not tell a caller that an order EXISTS but their
        // code was wrong — that turns it into an oracle for order numbers.
        SeedOrder(BuildOrder());
        var handler = CreateHandler();

        var wrongCode = await handler.Handle(
            new LookupOrder.Query(OrderNumber, MatchingEmail, "ZZZZZZ"), CancellationToken.None);
        var noSuchOrder = await handler.Handle(
            new LookupOrder.Query("CLS-2026-9999", MatchingEmail, RealCode), CancellationToken.None);

        Assert.False(wrongCode.IsSuccess);
        Assert.False(noSuchOrder.IsSuccess);
        Assert.Equal(noSuchOrder.Error?.Code, wrongCode.Error?.Code);
        Assert.Equal(noSuchOrder.Error?.Message, wrongCode.Error?.Message);
    }

    [Theory]
    [InlineData("a1b2c3")]
    [InlineData("A1b2C3")]
    [InlineData("A1B2C3")]
    public async Task Confirmation_Code_Is_Case_Insensitive(string typedCode)
    {
        // Generated uppercase, read off an e-mail, typed by a person.
        SeedOrder(BuildOrder());

        var result = await CreateHandler().Handle(
            new LookupOrder.Query(OrderNumber, MatchingEmail, typedCode), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Email_Stays_Case_Insensitive()
    {
        SeedOrder(BuildOrder());

        var result = await CreateHandler().Handle(
            new LookupOrder.Query(OrderNumber, "ALICE@EXAMPLE.COM", RealCode), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Wrong_Email_Still_Returns_Nothing()
    {
        SeedOrder(BuildOrder());

        var result = await CreateHandler().Handle(
            new LookupOrder.Query(OrderNumber, "attacker@evil.com", RealCode), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
