using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Dashboard;

/// <summary>
/// T-0104 (SEC-EMP-01) / ADR-0001 §D2 [OWN-DATA] — the inner ownership gate inside
/// <see cref="GetTimeAnalytics.Handler"/>. Mirrors <see cref="GetOrderAnalyticsHandlerTests"/>:
///   - AC2: a NON-admin caller's foreign <c>EmployeeId</c> is IGNORED — only the caller's own resolved
///     id reaches <see cref="IOrderRepository.GetCompletedOrdersByDateRangeAsync"/> (no time-spent /
///     efficiency leak);
///   - AC4: an Administrator caller's <c>Query.EmployeeId</c> IS honored;
///   - AC5: a non-admin with no resolvable employee gets
///     <see cref="BusinessErrorMessage.EmployeeNotFound"/>.
/// Written red → green per knowledge/testing.md (predates the handler fix).
/// </summary>
public class GetTimeAnalyticsHandlerTests
{
    private const string CallerEmployeeId = "emp-self-1";
    private const string ForeignEmployeeId = "emp-victim-2";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private readonly DateTime _start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _end = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    public GetTimeAnalyticsHandlerTests()
    {
        _orderRepository
            .Setup(r => r.GetCompletedOrdersByDateRangeAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());
    }

    private GetTimeAnalytics.Handler CreateHandler() =>
        (GetTimeAnalytics.Handler)Activator.CreateInstance(
            typeof(GetTimeAnalytics.Handler),
            _orderRepository.Object,
            _orderAccessService.Object,
            _session.Object)!;

    private void SetCaller(UserProfile role) =>
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));

    private GetTimeAnalytics.Query QueryFor(string? employeeId) =>
        new() { EmployeeId = employeeId, StartDate = _start, EndDate = _end };

    [Fact]
    public async Task NonAdmin_Supplying_Foreign_EmployeeId_Is_Scoped_To_Own_Id()
    {
        SetCaller(UserProfile.Employee);
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerEmployeeId);
        var handler = CreateHandler();

        var result = await handler.Handle(QueryFor(ForeignEmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _orderRepository.Verify(r => r.GetCompletedOrdersByDateRangeAsync(
            ForeignEmployeeId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _orderRepository.Verify(r => r.GetCompletedOrdersByDateRangeAsync(
            CallerEmployeeId, _start, _end, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Admin_Supplying_EmployeeId_Is_Honored()
    {
        SetCaller(UserProfile.Administrator);
        var handler = CreateHandler();

        var result = await handler.Handle(QueryFor(ForeignEmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _orderRepository.Verify(r => r.GetCompletedOrdersByDateRangeAsync(
            ForeignEmployeeId, _start, _end, It.IsAny<CancellationToken>()),
            Times.Once);
        _orderAccessService.Verify(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NonAdmin_With_No_Resolvable_Employee_Returns_EmployeeNotFound()
    {
        SetCaller(UserProfile.Employee);
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(QueryFor(ForeignEmployeeId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.EmployeeNotFound, result.Error!.Message);
        _orderRepository.Verify(r => r.GetCompletedOrdersByDateRangeAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
