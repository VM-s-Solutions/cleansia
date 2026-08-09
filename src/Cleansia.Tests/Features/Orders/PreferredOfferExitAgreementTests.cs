using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0045 D7.2 + PM ruling — <c>canChooseAnother</c> and the server's own refusal must agree on
/// <b>all five</b> terms, and the way to guarantee that is for the flag to BE the command's gate rather
/// than a copy of its terms.
///
/// <para>A flag reading <see langword="true"/> where the mechanism cannot deliver is this ADR's own
/// blocking finding (CH-F4). The fifth term — a recurring occurrence — was the same defect in miniature:
/// the command refuses one and the panel's ruled four-term list did not mention it, so the button would
/// have been offered to every recurring customer and refused every time.</para>
///
/// <para>Both verdicts are read from the REAL code paths — the customer's order-detail handler and the
/// re-offer command — so an agreement that held only inside a shared helper would still fail here.</para>
/// </summary>
public class PreferredOfferExitAgreementTests
{
    private const string OrderId = "order-exit-1";
    private const string CustomerUserId = "user-exit-customer";
    private const string FirstChoiceId = "employee-exit-first";
    private const string SecondChoiceId = "employee-exit-second";
    private const string RivalId = "employee-exit-rival";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderEmployeePayRepository = new();
    private readonly Mock<IOrderPhotoRepository> _orderPhotoRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();
    private readonly Mock<IPreferredCleanerHoldResolver> _resolver = new();

    public PreferredOfferExitAgreementTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
        _session
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Customer.ToString()));
        _orderAccessService.Setup(s => s.IsCustomerCaller()).Returns(true);
        _orderAccessService
            .Setup(s => s.CanBrowseOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // The order's own customer clears the STRICT gate too; the detail handler reads it to decide
        // whether this caller is entitled to the customer's own data.
        _orderAccessService
            .Setup(s => s.CanAccessOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _employeeRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Employee>().AsQueryable().BuildMock());
        _resolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? _, string? employeeId, string? _, DateTime _, int _, DateTime nowUtc,
                CancellationToken _) =>
                PreferredCleanerOutcome.Granted(
                    nowUtc.AddHours(2), new PreferredCleanerRecipient($"user-{employeeId}", null)));
    }

    public static TheoryData<string, bool> Terms => new()
    {
        { "open", true },
        { "round-cap-reached", false },
        { "already-has-a-cleaner", false },
        { "live-reservation", false },
        { "lead-time-too-short", false },
        { "recurring-occurrence", false },
    };

    [Theory]
    [MemberData(nameof(Terms))]
    public async Task The_Flag_And_The_Server_Agree(string scenario, bool expectedOpen)
    {
        Arrange(scenario);

        var detail = await CreateDetailHandler().Handle(new GetOrderDetails.Query(OrderId), default);
        var attempt = await CreateChooseHandler().Handle(
            new ChoosePreferredCleaner.Command(OrderId, SecondChoiceId), default);

        var flag = detail.Value!.PreferredOffer!.CanChooseAnother;
        var serverAccepted = attempt.IsSuccess;

        Assert.Equal(expectedOpen, flag);
        Assert.Equal(flag, serverAccepted);
        if (!serverAccepted)
        {
            Assert.Equal(BusinessErrorMessage.PreferredOfferClosed, attempt.Error!.Message);
        }
    }

    /// <summary>
    /// The deadline is disclosed only while it means something. An instant on a closed offer would be a
    /// promise about a reservation that has already ended.
    /// </summary>
    [Fact]
    public async Task Respond_By_Is_Disclosed_Only_While_The_Reservation_Is_Running()
    {
        Arrange("live-reservation");
        var live = await CreateDetailHandler().Handle(new GetOrderDetails.Query(OrderId), default);

        Arrange("open");
        var closed = await CreateDetailHandler().Handle(new GetOrderDetails.Query(OrderId), default);

        Assert.Equal(PreferredOfferState.AwaitingConfirmation, live.Value!.PreferredOffer!.State);
        Assert.NotNull(live.Value.PreferredOffer.RespondByUtc);
        Assert.Equal(PreferredOfferState.Closed, closed.Value!.PreferredOffer!.State);
        Assert.Null(closed.Value.PreferredOffer.RespondByUtc);
    }

    /// <summary>
    /// A cleaner must never learn an order was reserved for somebody else, and nobody is ever told they
    /// were passed over — so the block is customer-only, not merely customer-shaped.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Reading_The_Same_Order_Gets_No_Block_At_All()
    {
        Arrange("live-reservation");
        _orderAccessService.Setup(s => s.IsCustomerCaller()).Returns(false);
        _orderAccessService
            .Setup(s => s.CanAccessOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _session
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()));
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(RivalId);

        var detail = await CreateDetailHandler().Handle(new GetOrderDetails.Query(OrderId), default);

        Assert.Null(detail.Value!.PreferredOffer);
    }

    private GetOrderDetails.Handler CreateDetailHandler() =>
        new(
            _orderRepository.Object,
            _orderAccessService.Object,
            _session.Object,
            _payConfigRepository.Object,
            _orderEmployeePayRepository.Object,
            _orderPhotoRepository.Object,
            _employeeRepository.Object,
            _expressWaiverConsumer.Object);

    private ChoosePreferredCleaner.Handler CreateChooseHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            _resolver.Object,
            Mock.Of<INotificationProducer>());

    private void Arrange(string scenario)
    {
        var cleaningInHours = scenario == "lead-time-too-short" ? 5 : 48;
        var order = Order.Create(
            customerName: "Exit Customer",
            customerEmail: "exit@cleansia.test",
            customerPhone: "+420777999000",
            customerAddress: Address.Create("Exit St 1", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddHours(cleaningInHours),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerUserId,
            preferredEmployeeId: FirstChoiceId,
            recurringTemplateId: scenario == "recurring-occurrence" ? "tmpl-weekly" : null);
        order.Id = OrderId;
        order.UpdateEstimatedTime(120);
        order.SetMaxEmployees(2);
        order.SetCurrency(Cleansia.Core.Domain.Internationalization.Currency.Create("CZK", "Kč", "Czech Koruna", 1m));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        if (scenario == "live-reservation")
        {
            order.GrantPreferredHold(
                FirstChoiceId, DateTime.UtcNow.AddHours(2), DateTime.UtcNow,
                BookingPolicy.MaxPreferredOfferRounds);
        }
        else
        {
            var lapsedAt = DateTime.UtcNow.AddMinutes(-5);
            order.GrantPreferredHold(
                FirstChoiceId, lapsedAt, lapsedAt.AddHours(-2), BookingPolicy.MaxPreferredOfferRounds);
        }

        if (scenario == "round-cap-reached")
        {
            // Round two, granted after round one had already lapsed — the only ordering the aggregate
            // permits, and the one the customer's own exit produces.
            order.GrantPreferredHold(
                RivalId,
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddMinutes(-4),
                BookingPolicy.MaxPreferredOfferRounds);
        }

        if (scenario == "already-has-a-cleaner")
        {
            order.AddAssignedEmployee(OrderEmployee.Create(order, NewCleaner(RivalId)));
        }

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
    }

    private static Employee NewCleaner(string employeeId)
    {
        var user = User.CreateWithPassword(
            $"{employeeId}@cleansia.test", "Test-password-1!", "Rita", "Rival", UserProfile.Employee);
        user.Id = $"user-{employeeId}";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        return employee;
    }
}
