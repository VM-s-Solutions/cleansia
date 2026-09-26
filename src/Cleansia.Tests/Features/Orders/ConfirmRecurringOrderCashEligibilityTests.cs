using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration;
using Cleansia.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-24 at the last door a legacy cash occurrence can walk through: an occurrence
/// materialized before the rule, whose job needs two cleaners, is not confirmed as cash and is not
/// switched to card behind the customer's back. The refusal moves nothing; a paid occurrence keeps its
/// already-paid answer, so confirmed bookings are preserved.
/// </summary>
public sealed class ConfirmRecurringOrderCashEligibilityTests
{
    private const string OrderId = "order-recurring-cash-eligibility";
    private const string CustomerUserId = "user-recurring-cash-eligibility";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICreditAccountRepository> _creditAccounts = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IStripeClient> _stripe = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly AuditContext _audit = new();

    public ConfirmRecurringOrderCashEligibilityTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
    }

    private ConfirmRecurringOrder.Handler Handler() => new(
        OrderAccessDoubles.Over(_orderRepository, _session),
        _creditAccounts.Object,
        _users.Object,
        _session.Object,
        Mock.Of<ITenantProvider>(),
        _stripe.Object,
        new StripeConfig(new ConfigurationBuilder().Build()),
        _pending.Object,
        _notifications.Object,
        NoPreferredCleanerHold.Resolver,
        _adminNotifier.Object,
        _audit,
        NullLogger<ConfirmRecurringOrder.Handler>.Instance);

    private Order ArrangeOccurrence(
        PaymentType paymentType,
        int estimatedMinutes,
        PaymentStatus paymentStatus = PaymentStatus.Pending,
        int spareSeats = BookingPolicy.SpareSeatsPerOrder)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana.novakova@example.com",
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", "country-cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: paymentType,
            totalPrice: 990m,
            currencyId: "currency-czk",
            paymentStatus: paymentStatus,
            userId: CustomerUserId,
            recurringTemplateId: "tmpl-weekly-cash");
        order.Id = OrderId;
        order.TenantId = "company-of-the-order";
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        order.UpdateEstimatedTime(estimatedMinutes);
        order.CalculateRequiredEmployees(spareSeats);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        _orderRepository.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    [Theory]
    [InlineData(121)]
    [InlineData(360)]
    public async Task Confirming_A_Cash_Occurrence_That_Needs_Two_Cleaners_Is_Refused_And_Moves_Nothing(int minutes)
    {
        var order = ArrangeOccurrence(PaymentType.Cash, minutes);

        var result = await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderCashNotAvailable, result.Error!.Message);
        Assert.Equal(nameof(Order.PaymentType), result.Error.Code);
        Assert.Equal(PaymentType.Cash, order.PaymentType);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(order.StripePaymentIntentId);
        Assert.Equal(0m, order.CreditAppliedAmount);
        _pending.VerifyNoOtherCalls();
        _notifications.VerifyNoOtherCalls();
        _adminNotifier.VerifyNoOtherCalls();
        _stripe.VerifyNoOtherCalls();
        _creditAccounts.VerifyNoOtherCalls();
        Assert.Null(_audit.DrainSnapshot());
    }

    [Fact]
    public async Task A_Cash_Occurrence_On_A_One_Cleaner_Job_Confirms_As_Before()
    {
        var order = ArrangeOccurrence(PaymentType.Cash, 120);

        var result = await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.ClientSecret);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public async Task Spare_Seats_On_A_One_Cleaner_Cash_Occurrence_Do_Not_Refuse_The_Confirmation()
    {
        var order = ArrangeOccurrence(PaymentType.Cash, 120, spareSeats: 2);

        var result = await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.Equal(1, order.RequiredEmployees);
        Assert.Equal(3, order.MaxEmployees);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.ClientSecret);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public async Task A_Card_Occurrence_That_Needs_Two_Cleaners_Still_Takes_The_Card_Path()
    {
        var order = ArrangeOccurrence(PaymentType.Card, 121);
        var user = User.CreateWithPassword("jana.novakova@example.com", "Passw0rd!", "Jana", "Nováková");
        user.Id = CustomerUserId;
        user.AssignStripeCustomerId("cus_existing");
        _users.Setup(r => r.GetByIdAsync(CustomerUserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _stripe
            .Setup(s => s.CreatePaymentIntentAsync(
                It.IsAny<decimal>(), It.IsAny<string>(), "cus_existing", OrderId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult("pi_1", "pi_1_secret"));
        _stripe
            .Setup(s => s.CreateEphemeralKeyAsync("cus_existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ek_1");

        var result = await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("pi_1_secret", result.Value!.ClientSecret);
        Assert.Equal(2, order.RequiredEmployees);
    }

    [Fact]
    public async Task An_Already_Paid_Cash_Occurrence_Keeps_Its_Already_Paid_Answer()
    {
        ArrangeOccurrence(PaymentType.Cash, 121, PaymentStatus.Paid);

        var result = await Handler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderPaymentAlreadyPaid, result.Error!.Message);
    }
}
