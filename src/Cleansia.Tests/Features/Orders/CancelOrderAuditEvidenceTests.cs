using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Cleansia.Tests.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0062 D3 at the producer: a customer cancel emits ONE <see cref="CancelOrder.OrderCancellationEvidence"/>
/// carrying the figures the server charged by — the tier, the rate, the resolved free window, the
/// platform constants at that instant, the acceptance fact and the money — and nothing the customer
/// typed. A refused cancel emits nothing (the pipeline writes the refusal row from the key alone).
/// Enum members are pinned by camelCase NAME, the form the row is read in years later.
/// </summary>
public sealed class CancelOrderAuditEvidenceTests
{
    private const string OrderId = "order-ev-1";
    private const string UserId = "user-1";
    private const string CurrencyId = "currency-czk";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();
    private readonly AuditContext _auditContext = new();

    public CancelOrderAuditEvidenceTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
                BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{req.OrderId}:cancel", req.Amount, RefundStatus.Succeeded, false)));
    }

    private CancelOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            _refundService.Object,
            _creditAccountRepository.Object,
            _loyaltyService.Object,
            new CancellationPolicyResolver(_membershipRepository.Object),
            _producer.Object,
            _liveActivityProducer.Object,
            _expressWaiverConsumer.Object,
            _auditContext);

    private void ArrangePlusMember(int freeCancellationWindowHours = 4)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY", name: "Plus", discountPercentage: 5m,
            freeCancellationWindowHours: freeCancellationWindowHours, allowsExpressUpgrade: true);
        var membership = UserMembershipMockFactory.Paid(UserId, plan.Id);
        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!.Invoke(membership, [plan]);
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
    }

    private Order ArrangeOrder(
        DateTime cleaningUtc,
        decimal totalPrice = 1000m,
        bool accepted = true,
        string ownerUserId = UserId,
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        OrderStatus? currentStatus = null)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        currency.Id = CurrencyId;
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningUtc,
            paymentType: paymentType,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: ownerUserId);
        order.Id = OrderId;
        order.Created("tester", DateTime.UtcNow.AddDays(-2));
        order.SetCurrency(currency);
        order.AssignStripeSessionId("cs_test_ev");

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in new[] { OrderStatus.New, currentStatus ?? OrderStatus.Confirmed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("tester", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        if (accepted)
        {
            var cleanerUser = User.CreateWithPassword("cleaner@cleansia.test", "Passw0rd!", "Clean", "Er");
            cleanerUser.Id = "emp-ev-user";
            var cleaner = Employee.CreateWithUser(cleanerUser);
            cleaner.Id = "emp-ev";
            order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        }

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private static JsonElement Payload(AuditSnapshot? snapshot) => JsonDocument.Parse(snapshot!.AfterJson!).RootElement;

    [Fact]
    public void The_Marker_Is_Frozen_On_The_Order()
    {
        var descriptor = AuditActionDescriptor.For(typeof(CancelOrder.Command));

        Assert.Equal("customer.order.cancel", descriptor.Action);
        Assert.Equal("Order", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.False(descriptor.AllowsAnonymousActor);
    }

    [Fact]
    public async Task A_Plus_Member_Cancelling_An_Accepted_Paid_Job_Three_Hours_Out_Records_The_Last_Minute_Tier_And_Every_Figure()
    {
        ArrangePlusMember(freeCancellationWindowHours: 4);
        ArrangeOrder(DateTime.UtcNow.AddHours(3));

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, Reason: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("Order", snapshot!.ResourceType);
        Assert.Equal(OrderId, snapshot.ResourceId);
        Assert.Null(snapshot.BeforeJson);
        Assert.Null(snapshot.ActorUserId);

        var payload = Payload(snapshot);
        Assert.Equal("lastMinute", payload.GetProperty("tier").GetString());
        Assert.Equal(0.50m, payload.GetProperty("feeRate").GetDecimal());
        Assert.Equal(500m, payload.GetProperty("feeAmount").GetDecimal());
        Assert.Equal(500m, payload.GetProperty("refundAmount").GetDecimal());
        Assert.Equal(1000m, payload.GetProperty("totalPrice").GetDecimal());
        Assert.Equal(CurrencyId, payload.GetProperty("currencyId").GetString());
        Assert.True(payload.GetProperty("hasBeenAccepted").GetBoolean());
        Assert.InRange(payload.GetProperty("hoursBeforeCleaning").GetDecimal(), 2.9m, 3.0m);
        Assert.InRange(payload.GetProperty("minutesSinceBooking").GetDecimal(), 2879m, 2881m);
        Assert.Equal(4, payload.GetProperty("freeCancellationHoursApplied").GetInt32());
        Assert.False(payload.GetProperty("expressWaiverReleased").GetBoolean());
        Assert.True(payload.GetProperty("refundInitiated").GetBoolean());
        Assert.Equal("card", payload.GetProperty("paymentType").GetString());
        Assert.Equal("paid", payload.GetProperty("paymentStatus").GetString());
        Assert.False(payload.GetProperty("reasonProvided").GetBoolean());

        var figures = payload.GetProperty("policyFigures");
        Assert.Equal(BookingPolicy.FreeCancellationHours, figures.GetProperty("freeHours").GetInt32());
        Assert.Equal(BookingPolicy.PartialCancellationHours, figures.GetProperty("partialHours").GetInt32());
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, figures.GetProperty("partialRate").GetDecimal());
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, figures.GetProperty("lastMinuteRate").GetDecimal());
        Assert.Equal(BookingPolicy.OopsWindowMinutesStandard, figures.GetProperty("oopsMinutesStandard").GetInt32());
        Assert.Equal(BookingPolicy.OopsWindowMinutesFirstTime, figures.GetProperty("oopsMinutesFirstTime").GetInt32());

        var members = payload.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(16, members.Count);
        Assert.DoesNotContain(members, m =>
            m.EndsWith("Reason", StringComparison.OrdinalIgnoreCase)
            || m.EndsWith("Name", StringComparison.OrdinalIgnoreCase)
            || m.EndsWith("Email", StringComparison.OrdinalIgnoreCase)
            || m.EndsWith("Phone", StringComparison.OrdinalIgnoreCase)
            || m.EndsWith("Address", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_Standard_Customer_Cancelling_An_Unaccepted_Cash_Order_Records_The_Free_Tier_The_Waiver_Release_And_No_Refund()
    {
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        ArrangeOrder(DateTime.UtcNow.AddHours(3), accepted: false, paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, Reason: "changed my plans"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Payload(_auditContext.DrainSnapshot());
        Assert.Equal("freeNotAccepted", payload.GetProperty("tier").GetString());
        Assert.Equal(0m, payload.GetProperty("feeRate").GetDecimal());
        Assert.False(payload.GetProperty("hasBeenAccepted").GetBoolean());
        Assert.Equal(BookingPolicy.FreeCancellationHours, payload.GetProperty("freeCancellationHoursApplied").GetInt32());
        Assert.True(payload.GetProperty("expressWaiverReleased").GetBoolean());
        Assert.False(payload.GetProperty("refundInitiated").GetBoolean());
        Assert.Equal("cash", payload.GetProperty("paymentType").GetString());
        Assert.Equal("pending", payload.GetProperty("paymentStatus").GetString());
        Assert.True(payload.GetProperty("reasonProvided").GetBoolean());
        Assert.DoesNotContain("changed my plans", _auditContext.DrainSnapshot()?.AfterJson ?? string.Empty);
    }

    [Fact]
    public async Task The_Payment_Status_Recorded_Is_The_One_At_The_Click_Not_After_The_Refund()
    {
        ArrangePlusMember();
        var order = ArrangeOrder(DateTime.UtcNow.AddHours(3));
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
            {
                order.UpdatePaymentStatus(PaymentStatus.PartiallyRefunded);
                return BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{req.OrderId}:cancel", req.Amount, RefundStatus.Succeeded, false));
            });

        await CreateHandler().Handle(new CancelOrder.Command(OrderId, Reason: null), CancellationToken.None);

        Assert.Equal("paid", Payload(_auditContext.DrainSnapshot()).GetProperty("paymentStatus").GetString());
    }

    [Fact]
    public async Task An_InProgress_Order_Is_Refused_With_The_Key_And_Records_No_Evidence()
    {
        ArrangeOrder(DateTime.UtcNow.AddHours(-1), currentStatus: OrderStatus.InProgress);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, Reason: null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
    }

    [Fact]
    public async Task A_Cross_User_Probe_Is_Refused_As_Not_Found_Leaves_The_Order_Untouched_And_Records_No_Evidence()
    {
        var victim = ArrangeOrder(DateTime.UtcNow.AddHours(3), ownerUserId: "someone-else");

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, Reason: null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.Equal(OrderStatus.Confirmed, victim.CurrentStatus);
        Assert.Null(victim.CancelledAt);
        Assert.Null(_auditContext.DrainSnapshot());
    }
}
