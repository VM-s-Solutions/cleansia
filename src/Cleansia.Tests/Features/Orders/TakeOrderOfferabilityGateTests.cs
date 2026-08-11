using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using FluentValidation.Results;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0037 D6 + D6.1 — the take gate. <c>TakeOrder</c> was the only cleaner-facing order command
/// with no status rule, so a stale client could assign a cleaner to a Cancelled or Completed job
/// that still had a free seat, burning one of their 3/6/10 weekly slots on a dead order that does
/// not even block their calendar.
///
/// <para>The refusal is a taxonomy, not one key: the two terminal states carry the take gate's own
/// <c>order.take.*</c> keys — a cleaner and a customer need different sentences for the same fact, and
/// iOS resolves every <c>error.*</c> string from one shared catalog — and everything else the money
/// axis refuses gets one opaque residue key that names no part of the customer's payment.</para>
///
/// <para><b>TC-TAKE-ONE-ERROR</b> is the second half and it is structural, not cosmetic. The
/// validator used to carry two <c>RuleFor</c> chains and <c>Cascade.Stop</c> is rule-level, so both
/// always ran: an unknown order id returned <c>order.not_found; order.time_conflict</c> — a
/// semicolon-joined composite no client can resolve — while an ADR-0036 HELD order returned a bare
/// <c>order.not_found</c>. Existence was inferable from the pairing, which is exactly what folding
/// the hold into the existence rule exists to prevent. One ordered chain, one error per refusal.
/// A second <c>RuleFor</c> in that file re-opens it and this class goes red.</para>
/// </summary>
public class TakeOrderOfferabilityGateTests
{
    private const string OrderId = "order-gate-1";
    private const string EmployeeId = "emp-gate-1";
    private const string OtherEmployeeId = "emp-gate-2";
    private const string RecurringTemplateId = "tpl-weekly-gate";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly TakeOrder.Validator _validator;

    public TakeOrderOfferabilityGateTests()
    {
        _validator = new TakeOrder.Validator(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object);
    }

    // ── The offerability gate itself ──

    [Fact]
    public async Task A_Cancelled_Order_With_A_Free_Seat_Is_Refused()
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.Cancelled, maxEmployees: 2));

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.TakeOrderAlreadyCancelled);
    }

    [Fact]
    public async Task A_Completed_Order_With_A_Free_Seat_Is_Refused()
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.Completed, maxEmployees: 2));

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.TakeOrderAlreadyCompleted);
    }

    [Theory]
    // Checkout is open or abandoned; the 15-minute sweep cancels it within ~1h15m.
    [InlineData(OrderStatus.New, PaymentType.Card, PaymentStatus.Pending, null)]
    // An admin pushed it forward, or a decline left it Pending for retry — the same sweep has no
    // OrderStatus term, so it cancels this one out from under an assigned cleaner.
    [InlineData(OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Pending, null)]
    // The customer has not confirmed this occurrence; the hourly sweep retracts it at T-1h.
    [InlineData(OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId)]
    [InlineData(OrderStatus.OnTheWay, PaymentType.Cash, PaymentStatus.Paid, null)]
    [InlineData(OrderStatus.InProgress, PaymentType.Cash, PaymentStatus.Paid, null)]
    [InlineData(OrderStatus.Pending, PaymentType.Cash, PaymentStatus.Paid, null)]
    public async Task A_Non_Offerable_Order_Is_Refused_With_The_Opaque_Residue_Key(
        OrderStatus status,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId)
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, status, maxEmployees: 2, paymentType, paymentStatus, recurringTemplateId));

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.OrderNotTakeable);
    }

    [Theory]
    // The cash pipeline's canonical pre-take state: the take IS the confirmation.
    [InlineData(OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, null)]
    [InlineData(OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid, null)]
    // A confirmed recurring occurrence, cash included, is Paid the moment the customer confirms.
    [InlineData(OrderStatus.Confirmed, PaymentType.Cash, PaymentStatus.Paid, RecurringTemplateId)]
    public async Task An_Offerable_Order_Still_Passes(
        OrderStatus status,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId)
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(
            OrderId, status, maxEmployees: 2, paymentType, paymentStatus, recurringTemplateId));

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    // ── TC-TAKE-ONE-ERROR: every refusal returns exactly one error ──

    /// <summary>
    /// The live defect this closes: <c>NotHaveTimeConflictAsync</c> returns false for a null order,
    /// so an unknown id produced <c>order.not_found; order.time_conflict</c> in production. An
    /// ADR-0036 HELD order folds its refusal into this same existence rule, so it takes the same
    /// path and the pairing that leaked existence is gone with it.
    /// </summary>
    [Fact]
    public async Task An_Unknown_Order_Returns_Exactly_One_Error()
    {
        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(Array.Empty<Order>().AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.OrderNotFound);
    }

    [Fact]
    public async Task An_Empty_Order_Id_Returns_Exactly_One_Error()
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 2));

        var result = await _validator.ValidateAsync(new TakeOrder.Command(string.Empty));

        AssertSingleError(result, BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task A_Full_Order_Returns_Exactly_One_Error()
    {
        Arrange(ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Confirmed, OtherEmployeeId, maxEmployees: 1));

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.NoAvailableSpots);
    }

    /// <summary>
    /// Offerability is evaluated BEFORE seats. For a dead job that is also full, "this job is no
    /// longer available" is the true answer and "no available spots" is the misleading one — the
    /// cleaner would refresh and keep waiting for a seat that is never coming back.
    /// </summary>
    [Fact]
    public async Task A_Cancelled_Order_That_Is_Also_Full_Answers_On_The_Status_Not_The_Seat()
    {
        Arrange(ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Cancelled, OtherEmployeeId, maxEmployees: 1));

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.TakeOrderAlreadyCancelled);
    }

    [Fact]
    public async Task An_Unapproved_Cleaner_Returns_Exactly_One_Error()
    {
        Arrange(
            ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 2),
            employeeStatus: ContractStatus.Rejected);

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.EmployeeNotApproved);
    }

    [Fact]
    public async Task An_Already_Assigned_Cleaner_Returns_Exactly_One_Error()
    {
        Arrange(ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Confirmed, EmployeeId, maxEmployees: 2));

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.EmployeeAlreadyAssignedToOrder);
    }

    [Fact]
    public async Task A_Cleaner_At_Their_Weekly_Cap_Returns_Exactly_One_Error()
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 2), weeklyCount: 99);

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.WeeklyOrderLimitReached);
    }

    [Fact]
    public async Task A_Calendar_Clash_Returns_Exactly_One_Error()
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 2), overlaps: true);

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId));

        AssertSingleError(result, BusinessErrorMessage.TimeConflict);
    }

    private static void AssertSingleError(ValidationResult result, string expectedMessage)
    {
        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(expectedMessage, failure.ErrorMessage);
    }

    private void Arrange(
        Order order,
        ContractStatus employeeStatus = ContractStatus.Approved,
        int weeklyCount = 0,
        bool overlaps = false)
    {
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, employeeStatus, withAddress: true);

        _orderRepository.Setup(r => r.ExistsAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetEmployeeOrderCountThisWeekAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(weeklyCount);
        _orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(
                EmployeeId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(overlaps);

        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
    }
}
