using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28: an administrator records a cash handover the cleaner could not — who took
/// it, when and how much — and the order is paid exactly as the cleaner's own record would have paid it.
/// On an order an administrator already completed, both of the receipt's dates now exist, so its receipt
/// is issued here. Every refusal is the validator's, as MarkCashCollected's are.
/// </summary>
public class AdminRecordCashReceivedTests
{
    private const string OrderId = "order-admin-cash-1";
    private const string CleanerId = "emp-admin-cash-1";
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    private AdminRecordCashReceived.Handler Handler() => new(_orderRepository.Object, _pending.Object);

    private AdminRecordCashReceived.Validator Validator()
    {
        _orderRepository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new AdminRecordCashReceived.Validator(_orderRepository.Object, new StubTimeProvider(Now));
    }

    private static AdminRecordCashReceived.Command Command(
        string employeeId = CleanerId, decimal amount = 1000m, DateTime? receivedAt = null) =>
        new(OrderId, employeeId, receivedAt ?? Now.AddMinutes(-30), amount);

    private Order ArrangeOrder(
        PaymentType paymentType = PaymentType.Cash,
        PaymentStatus paymentStatus = PaymentStatus.Pending,
        params OrderStatus[] history)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Hlavní 2", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: Now.AddHours(-2),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: "owner-user");
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee(CleanerId, ContractStatus.Approved)));
        foreach (var status in history.Length == 0 ? [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress] : history)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        }

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    [Fact]
    public async Task The_Handover_Is_Recorded_As_Stated_And_The_Order_Is_Paid()
    {
        var order = ArrangeOrder();
        var receivedAt = Now.AddMinutes(-45);

        var result = await Handler().Handle(Command(amount: 950m, receivedAt: receivedAt), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(CleanerId, order.CollectedByEmployeeId);
        Assert.Equal(receivedAt, order.CashCollectedAt);
        Assert.Equal(950m, order.CashCollectedAmount);
        // Still in progress: the cleaner's completion issues the receipt.
        _pending.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task On_An_Order_Already_Completed_The_Receipt_Is_Issued()
    {
        ArrangeOrder(history: [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed]);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _pending.Verify(p => p.Enqueue(
            QueueNames.GenerateReceipt, It.IsAny<It.IsAnyType>(), MessageKeys.Receipt(OrderId)), Times.Once);
    }

    public static TheoryData<PaymentType, PaymentStatus, OrderStatus[], string, string> Refusals => new()
    {
        { PaymentType.Cash, PaymentStatus.Pending, [OrderStatus.New, OrderStatus.Cancelled], CleanerId, BusinessErrorMessage.OrderAlreadyCancelled },
        { PaymentType.Cash, PaymentStatus.Pending, [OrderStatus.New, OrderStatus.Confirmed], CleanerId, BusinessErrorMessage.OrderNotInProgress },
        { PaymentType.Card, PaymentStatus.Pending, [OrderStatus.New, OrderStatus.InProgress], CleanerId, BusinessErrorMessage.OrderCashNotAllowedOnCardOrder },
        { PaymentType.Cash, PaymentStatus.Paid, [OrderStatus.New, OrderStatus.InProgress], CleanerId, BusinessErrorMessage.OrderCashAlreadyCollected },
        { PaymentType.Cash, PaymentStatus.Refunded, [OrderStatus.New, OrderStatus.InProgress], CleanerId, BusinessErrorMessage.OrderPaymentNotOutstanding },
        { PaymentType.Cash, PaymentStatus.Pending, [OrderStatus.New, OrderStatus.InProgress], "somebody-else", BusinessErrorMessage.EmployeeNotAssignedToOrder },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_Handover_That_Cannot_Have_Happened_Is_Refused(
        PaymentType paymentType, PaymentStatus paymentStatus, OrderStatus[] history, string employeeId, string error)
    {
        ArrangeOrder(paymentType, paymentStatus, history);

        var result = await Validator().ValidateAsync(Command(employeeId: employeeId));

        Assert.Equal(error, Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.005)]
    public async Task An_Amount_That_Is_Not_Money_Is_Refused(decimal amount)
    {
        ArrangeOrder();

        var result = await Validator().ValidateAsync(Command(amount: amount));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCashAmountInvalid);
    }

    [Fact]
    public async Task A_Handover_In_The_Future_Is_Refused()
    {
        ArrangeOrder();

        var result = await Validator().ValidateAsync(Command(receivedAt: Now.AddMinutes(5)));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.OrderCashReceivedAtInFuture);
    }

    /// <summary>
    /// A request that leaves the time out binds 0001-01-01, which is not in the future and would print on
    /// the fiscal receipt as the handover date.
    /// </summary>
    [Fact]
    public async Task A_Handover_With_No_Time_Is_Refused()
    {
        ArrangeOrder();

        var result = await Validator().ValidateAsync(new AdminRecordCashReceived.Command(OrderId, CleanerId, default, 1000m));

        Assert.Equal(BusinessErrorMessage.Required, Assert.Single(result.Errors).ErrorMessage);
    }

    /// <summary>The cash changes hands at the door, which is no earlier than the cleaner may start.</summary>
    [Fact]
    public async Task A_Handover_Before_The_Clean_Could_Begin_Is_Refused()
    {
        var order = ArrangeOrder();
        var earliest = order.CleaningDateTime.AddMinutes(-BookingPolicy.StartGraceWindowMinutes);

        var tooEarly = await Validator().ValidateAsync(Command(receivedAt: earliest.AddMinutes(-1)));
        var atTheDoor = await Validator().ValidateAsync(Command(receivedAt: earliest));

        Assert.Equal(BusinessErrorMessage.OrderCashReceivedAtBeforeClean, Assert.Single(tooEarly.Errors).ErrorMessage);
        Assert.True(atTheDoor.IsValid, string.Join(", ", atTheDoor.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task A_Well_Formed_Handover_Passes_The_Validator()
    {
        ArrangeOrder();

        var result = await Validator().ValidateAsync(Command());

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
