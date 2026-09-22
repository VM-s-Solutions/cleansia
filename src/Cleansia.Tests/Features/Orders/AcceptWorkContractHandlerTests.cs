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
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0068 D3 (Verification #4) — the standalone acceptance for a cleaner an administrator placed: any
/// not-over order is admitted (a cleaner placed on an in-progress job must be able to accept before
/// completing), a stranger to the crew is refused before the text is looked at, a text of another
/// document is a mismatch, and a seat that already has its row answers success with that row and
/// writes nothing.
/// </summary>
public sealed class AcceptWorkContractHandlerTests
{
    private const string OrderId = "order-accept-1";
    private const string EmployeeId = "emp-accept-1";
    private const string StrangerId = "emp-accept-stranger";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<IWorkContractAcceptanceRepository> _acceptanceRepository = new();
    private readonly Mock<IWorkContractAcceptor> _workContractAcceptor = new();

    private AcceptWorkContract.Validator CreateValidator() =>
        new(_orderRepository.Object, _accessService.Object, WorkContractTestData.LegalDocumentRepository().Object);

    private AcceptWorkContract.Handler CreateHandler() =>
        new(_orderRepository.Object, _accessService.Object, _acceptanceRepository.Object, _workContractAcceptor.Object);

    private Order Arrange(OrderStatus status, string caller = EmployeeId, params WorkContractAcceptance[] existingRows)
    {
        var order = ValidatorTestHelpers.BuildOrder(OrderId, status, EmployeeId);
        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _acceptanceRepository.Setup(r => r.GetQueryable()).Returns(existingRows.AsQueryable().BuildMock());
        return order;
    }

    private static void AssertSingleError(ValidationResult result, string expectedMessage)
    {
        Assert.False(result.IsValid);
        var failure = Assert.Single(result.Errors);
        Assert.Equal(expectedMessage, failure.ErrorMessage);
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.OnTheWay)]
    [InlineData(OrderStatus.InProgress)]
    public async Task A_Crew_Member_On_A_Job_That_Is_Not_Over_May_Accept(OrderStatus status)
    {
        Arrange(status);

        var result = await CreateValidator().ValidateAsync(new AcceptWorkContract.Command(OrderId, WorkContractTestData.TextIdEn));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task A_Completed_Job_Refuses_The_Acceptance()
    {
        Arrange(OrderStatus.Completed);

        var result = await CreateValidator().ValidateAsync(new AcceptWorkContract.Command(OrderId, WorkContractTestData.TextIdEn));

        AssertSingleError(result, BusinessErrorMessage.TakeOrderAlreadyCompleted);
    }

    [Fact]
    public async Task A_Cancelled_Job_Refuses_The_Acceptance()
    {
        Arrange(OrderStatus.Cancelled);

        var result = await CreateValidator().ValidateAsync(new AcceptWorkContract.Command(OrderId, WorkContractTestData.TextIdEn));

        AssertSingleError(result, BusinessErrorMessage.TakeOrderAlreadyCancelled);
    }

    [Fact]
    public async Task A_Cleaner_Not_On_The_Crew_Is_Refused_Before_The_Status_Or_The_Text_Is_Read()
    {
        Arrange(OrderStatus.Completed, caller: StrangerId);

        var result = await CreateValidator().ValidateAsync(new AcceptWorkContract.Command(OrderId, WorkContractTestData.OtherTextId));

        AssertSingleError(result, BusinessErrorMessage.EmployeeNotAssignedToOrder);
    }

    [Fact]
    public async Task No_Text_Id_Is_Refused_As_Not_Accepted_Before_Existence()
    {
        var result = await CreateValidator().ValidateAsync(new AcceptWorkContract.Command("order-nowhere", " "));

        AssertSingleError(result, BusinessErrorMessage.WorkContractNotAccepted);
    }

    [Fact]
    public async Task A_Missing_Order_Is_Not_Found()
    {
        _orderRepository.Setup(r => r.ExistsAsync("order-nowhere", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(new AcceptWorkContract.Command("order-nowhere", WorkContractTestData.TextIdEn));

        AssertSingleError(result, BusinessErrorMessage.OrderNotFound);
    }

    [Fact]
    public async Task A_Text_Of_Another_Document_Is_A_Mismatch()
    {
        Arrange(OrderStatus.Confirmed);

        var result = await CreateValidator().ValidateAsync(new AcceptWorkContract.Command(OrderId, WorkContractTestData.OtherTextId));

        AssertSingleError(result, BusinessErrorMessage.WorkContractTextMismatch);
    }

    [Fact]
    public async Task The_Handler_Stages_A_Row_For_The_Callers_Seat()
    {
        var order = Arrange(OrderStatus.Confirmed);
        var seat = order.AssignedEmployees.Single();
        var staged = WorkContractAcceptance.Create(
            OrderId, seat.Id, EmployeeId, WorkContractTestData.Document().TextFor("en")!, WorkContractTestData.Version,
            "cleansia.partner", null, null, null, "{}");
        _workContractAcceptor
            .Setup(a => a.StageAsync(It.Is<Order>(o => o.Id == OrderId), It.Is<OrderEmployee>(oe => oe.Id == seat.Id), WorkContractTestData.TextIdEn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staged);

        var result = await CreateHandler().Handle(new AcceptWorkContract.Command(OrderId, WorkContractTestData.TextIdEn), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(staged.Id, result.Value!.AcceptanceId);
        Assert.Equal(OrderId, result.Value.OrderId);
        Assert.Equal(WorkContractTestData.Version, result.Value.DocumentVersion);
        Assert.Equal(staged.AcceptedOn, result.Value.AcceptedOn);
        _workContractAcceptor.VerifyAll();
    }

    [Fact]
    public async Task A_Seat_That_Already_Has_Its_Row_Answers_That_Row_And_Writes_Nothing()
    {
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Confirmed, EmployeeId);
        var seat = order.AssignedEmployees.Single();
        var existing = WorkContractAcceptance.Create(
            OrderId, seat.Id, EmployeeId, WorkContractTestData.Document().TextFor("en")!, WorkContractTestData.Version,
            "cleansia.partner", null, null, null, "{}");
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
        _acceptanceRepository.Setup(r => r.GetQueryable()).Returns(new[] { existing }.AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(new AcceptWorkContract.Command(OrderId, WorkContractTestData.TextIdEn), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value!.AcceptanceId);
        _workContractAcceptor.Verify(
            a => a.StageAsync(It.IsAny<Order>(), It.IsAny<OrderEmployee>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_Row_On_Another_Seat_Of_The_Same_Cleaner_Does_Not_Count()
    {
        var order = Arrange(OrderStatus.Confirmed, EmployeeId,
            WorkContractAcceptance.Create(
                OrderId, "01OLDSEAT00000000000000001", EmployeeId, WorkContractTestData.Document().TextFor("en")!,
                WorkContractTestData.Version, "cleansia.partner", null, null, null, "{}"));
        var seat = order.AssignedEmployees.Single();
        _workContractAcceptor
            .Setup(a => a.StageAsync(It.IsAny<Order>(), It.Is<OrderEmployee>(oe => oe.Id == seat.Id), WorkContractTestData.TextIdEn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkContractAcceptance.Create(
                OrderId, seat.Id, EmployeeId, WorkContractTestData.Document().TextFor("en")!, WorkContractTestData.Version,
                "cleansia.partner", null, null, null, "{}"));

        var result = await CreateHandler().Handle(new AcceptWorkContract.Command(OrderId, WorkContractTestData.TextIdEn), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _workContractAcceptor.VerifyAll();
    }
}
