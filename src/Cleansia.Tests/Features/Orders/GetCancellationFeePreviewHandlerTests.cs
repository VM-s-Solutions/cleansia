using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// T-0526 — the preview's own contract: who may read it, when it refuses, and the express-waiver
/// disclosure ADR-0035 AM-13 requires. The money agreement with the cancel path lives in
/// <see cref="CancellationFeePreviewAgreementTests"/>; this suite covers the boundaries around it.
/// </summary>
public class GetCancellationFeePreviewHandlerTests
{
    private const string OrderId = "order-preview-2";
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();

    public GetCancellationFeePreviewHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    private GetCancellationFeePreview.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            new CancellationPolicyResolver(_membershipRepository.Object),
            _expressWaiverConsumer.Object);

    private Order ArrangeOrder(
        double cleaningInHours = 12,
        bool assignCleaner = true,
        string ownerId = UserId,
        OrderStatus[]? statuses = null)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddHours(cleaningInHours),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: ownerId);
        order.Id = OrderId;
        order.Created("tester", DateTime.UtcNow.AddDays(-2));
        order.SetCurrency(currency);

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in statuses ?? [OrderStatus.New, OrderStatus.Confirmed])
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("tester", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        if (assignCleaner)
        {
            var cleanerUser = User.CreateWithPassword("cleaner@cleansia.test", "Passw0rd!", "Clean", "Er");
            cleanerUser.Id = "emp-preview2-user";
            var cleaner = Employee.CreateWithUser(cleanerUser);
            cleaner.Id = "emp-preview2";
            order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        }

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult<GetCancellationFeePreview.Response>> PreviewAsync(
        string orderId = OrderId) =>
        CreateHandler().Handle(new GetCancellationFeePreview.Query(orderId), CancellationToken.None);

    // ── AC6: ownership, and the same shape a missing order returns ──

    [Fact]
    public async Task Another_Customers_Order_Is_Not_Found()
    {
        ArrangeOrder(ownerId: OtherUserId);

        var result = await PreviewAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task An_Unknown_Order_Is_Not_Found()
    {
        ArrangeOrder();

        var result = await PreviewAsync("order-that-does-not-exist");

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task A_Cross_Customer_Read_Is_Indistinguishable_From_A_Missing_Order()
    {
        // Identical error, identical field: the preview must not become an existence oracle for other
        // customers' bookings.
        ArrangeOrder(ownerId: OtherUserId);
        var foreign = await PreviewAsync();
        var missing = await PreviewAsync("order-that-does-not-exist");

        Assert.Equal(missing.Error!.Code, foreign.Error!.Code);
        Assert.Equal(missing.Error.Message, foreign.Error.Message);
    }

    // ── The states where there is nothing to quote — the cancel path's own refusals ──

    [Theory]
    [InlineData(OrderStatus.Cancelled, BusinessErrorMessage.OrderAlreadyCancelled)]
    [InlineData(OrderStatus.Completed, BusinessErrorMessage.OrderAlreadyCompleted)]
    [InlineData(OrderStatus.InProgress, BusinessErrorMessage.OrderInProgressCannotCancel)]
    public async Task An_Uncancellable_Order_Is_Refused_With_The_Cancel_Paths_Own_Reason(
        OrderStatus status, string expectedError)
    {
        ArrangeOrder(statuses: [OrderStatus.New, OrderStatus.Confirmed, status]);

        var result = await PreviewAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error!.Message);
    }

    // ── ADR-0035 AM-13: the express-waiver forfeiture ──

    [Fact]
    public async Task Forfeiting_A_Live_Express_Waiver_Is_Disclosed()
    {
        ArrangeOrder();
        _expressWaiverConsumer
            .Setup(c => c.WouldForfeitOnCustomerCancelAsync(
                OrderId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await PreviewAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.ExpressWaiverForfeitedOnCancel);
    }

    [Fact]
    public async Task The_Waiver_Question_Is_Asked_With_The_Same_Acceptance_Predicate_The_Fee_Uses()
    {
        // The release rule keys on "was a cleaner ever pulled onto this job", the same fact that prices
        // the fee. Asking it with a different answer would disclose a forfeiture that will not happen,
        // or hide one that will.
        ArrangeOrder(assignCleaner: true);

        await PreviewAsync();

        _expressWaiverConsumer.Verify(
            c => c.WouldForfeitOnCustomerCancelAsync(OrderId, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task With_No_Cleaner_Assigned_The_Waiver_Question_Is_Asked_With_False()
    {
        ArrangeOrder(assignCleaner: false);

        var result = await PreviewAsync();

        _expressWaiverConsumer.Verify(
            c => c.WouldForfeitOnCustomerCancelAsync(OrderId, false, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.False(result.Value!.ExpressWaiverForfeitedOnCancel);
    }

    [Fact]
    public async Task A_Zero_Fee_Cancellation_Still_Discloses_The_Forfeiture()
    {
        // The case the disclosure exists for: two days out the fee is 0, so a client rendering only the
        // money would show nothing at all while a paid-for credit disappears.
        ArrangeOrder(cleaningInHours: 48);
        _expressWaiverConsumer
            .Setup(c => c.WouldForfeitOnCustomerCancelAsync(
                OrderId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await PreviewAsync();

        Assert.Equal(0m, result.Value!.FeeRate);
        Assert.True(result.Value.ExpressWaiverForfeitedOnCancel);
    }

    // ── The response body ──

    [Fact]
    public async Task The_Response_Carries_The_Orders_Total_And_Currency()
    {
        ArrangeOrder();

        var result = await PreviewAsync();

        Assert.Equal(OrderId, result.Value!.OrderId);
        Assert.Equal(1000m, result.Value.TotalPrice);
        Assert.Equal("CZK", result.Value.CurrencyCode);
    }

    [Fact]
    public async Task The_Fee_And_The_Refund_Always_Sum_To_The_Total()
    {
        ArrangeOrder(cleaningInHours: 12);

        var result = await PreviewAsync();

        Assert.Equal(CancellationFeeTier.Partial, result.Value!.Tier);
        Assert.Equal(result.Value.TotalPrice, result.Value.FeeAmount + result.Value.RefundAmount);
    }
}
