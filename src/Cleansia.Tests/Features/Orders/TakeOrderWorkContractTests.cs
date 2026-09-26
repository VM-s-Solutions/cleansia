using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;
using Cleansia.Tests.Infrastructure;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0068 D3 (Verification #3) — the take's contract rules and their PLACE in the one ordered chain:
/// the tick sits before existence, so a held and a missing order answer the same refusal and no
/// existence leaks through the pairing; the echo sits last, so every better refusal wins over "wrong
/// text"; and the handler stages the row between the seat and the commit, so a refused take stages
/// nothing.
/// </summary>
public sealed class TakeOrderWorkContractTests
{
    private const string OrderId = "order-wc-1";
    private const string EmployeeId = "emp-wc-1";
    private const string OtherEmployeeId = "emp-wc-2";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IWorkContractAcceptor> _workContractAcceptor = new();

    private TakeOrder.Validator CreateValidator() =>
        new(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object,
            ValidatorTestHelpers.CurrencyResolver(),
            WorkContractTestData.LegalDocumentRepository().Object);

    private TakeOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object,
            _notificationProducer.Object,
            _emailService.Object,
            TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
            _workContractAcceptor.Object,
            NullLogger<TakeOrder.Handler>.Instance);

    private void ArrangeTakeable(Order order)
    {
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, ContractStatus.Approved, withAddress: true);

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(EmployeeId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderRepository
            .Setup(r => r.GetLiveReservationsForBeneficiaryInWindowAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
    }

    private static void AssertSingleError(ValidationResult result, string expectedMessage)
    {
        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(expectedMessage, failure.ErrorMessage);
    }

    // ── The tick, before existence ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_Take_Without_A_Text_Id_Is_Refused_As_Not_Accepted(string? textId)
    {
        ArrangeTakeable(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New));

        var result = await CreateValidator().ValidateAsync(new TakeOrder.Command(OrderId, textId));

        AssertSingleError(result, BusinessErrorMessage.WorkContractNotAccepted);
    }

    [Fact]
    public async Task A_Missing_Order_And_A_Held_Order_Answer_The_Same_Refusal_Without_A_Text_Id()
    {
        var held = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New);
        held.GrantPreferredHold(OtherEmployeeId, DateTime.UtcNow.AddHours(6), DateTime.UtcNow, 3);
        ArrangeTakeable(held);

        var heldResult = await CreateValidator().ValidateAsync(new TakeOrder.Command(OrderId, null));
        var missingResult = await CreateValidator().ValidateAsync(new TakeOrder.Command("order-nowhere", null));

        AssertSingleError(heldResult, BusinessErrorMessage.WorkContractNotAccepted);
        AssertSingleError(missingResult, BusinessErrorMessage.WorkContractNotAccepted);
    }

    // ── The echo, last ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Text_Of_The_Orders_Own_Document_In_Another_Language_Is_Accepted()
    {
        ArrangeTakeable(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New));

        var result = await CreateValidator().ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdCs));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task A_Text_Of_Another_Document_Is_Refused_As_A_Mismatch()
    {
        ArrangeTakeable(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New));

        var result = await CreateValidator().ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.OtherTextId));

        AssertSingleError(result, BusinessErrorMessage.WorkContractTextMismatch);
    }

    [Fact]
    public async Task An_Unknown_Text_Id_Is_Refused_As_A_Mismatch()
    {
        ArrangeTakeable(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New));

        var result = await CreateValidator().ValidateAsync(new TakeOrder.Command(OrderId, "01TEXT0000000000000NOWHERE"));

        AssertSingleError(result, BusinessErrorMessage.WorkContractTextMismatch);
    }

    [Fact]
    public async Task An_Order_With_No_Document_Matches_No_Text()
    {
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New);
        Assert.NotNull(order.WorkContractDocumentId);
        var unstamped = Order.Create(
            "Test Customer", "test@example.com", "+420000000000",
            Core.Domain.Users.Address.Create("123 Main St", "Prague", "11000", "cz"),
            1, 1, ValidatorTestHelpers.DefaultCleaningTime, PaymentType.Cash, 1000m, ValidatorTestHelpers.CurrencyId, PaymentStatus.Pending);
        unstamped.Id = OrderId;
        unstamped.SetMaxEmployees(2);
        unstamped.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, unstamped));
        ArrangeTakeable(unstamped);

        var result = await CreateValidator().ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn));

        Assert.Null(unstamped.WorkContractDocumentId);
        AssertSingleError(result, BusinessErrorMessage.WorkContractTextMismatch);
    }

    [Fact]
    public async Task A_Full_Order_And_A_Wrong_Text_Answer_No_Available_Spots_Because_The_Echo_Is_Last()
    {
        ArrangeTakeable(ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Confirmed, OtherEmployeeId, maxEmployees: 1));

        var result = await CreateValidator().ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.OtherTextId));

        AssertSingleError(result, BusinessErrorMessage.NoAvailableSpots);
    }

    // ── The handler: staged between the seat and the commit ─────────────────────────────────────

    [Fact]
    public async Task A_Won_Seat_Is_Staged_With_Its_Acceptance_Before_The_Commit()
    {
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.New, OtherEmployeeId, maxEmployees: 2);
        ArrangeTakeable(order);
        var calls = new List<string>();
        _workContractAcceptor
            .Setup(a => a.StageAsync(order, It.IsAny<OrderEmployee>(), WorkContractTestData.TextIdEn, It.IsAny<CancellationToken>()))
            .Callback<Order, OrderEmployee, string, CancellationToken>((o, seat, _, _) =>
            {
                calls.Add("stage");
                Assert.Equal(EmployeeId, seat.EmployeeId);
                Assert.Contains(seat, o.AssignedEmployees);
            })
            .ReturnsAsync((Order o, OrderEmployee seat, string text, CancellationToken _) =>
                WorkContractAcceptance.Create(o.Id, seat.Id, seat.EmployeeId, WorkContractTestData.Document().TextFor("en")!,
                    WorkContractTestData.Version, "cleansia.partner", null, null, null, "{}"));
        _orderRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("commit"))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["stage", "commit"], calls);
    }

    [Fact]
    public async Task A_Refused_Take_Stages_No_Acceptance()
    {
        ArrangeTakeable(ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Confirmed, OtherEmployeeId, maxEmployees: 1));

        var result = await CreateHandler().Handle(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.NoAvailableSpots, result.Error!.Message);
        _workContractAcceptor.Verify(
            a => a.StageAsync(It.IsAny<Order>(), It.IsAny<OrderEmployee>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
