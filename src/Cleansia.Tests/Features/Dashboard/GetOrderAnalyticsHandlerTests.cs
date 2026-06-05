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
/// <see cref="GetOrderAnalytics.Handler"/>. The coarse <c>CanGetCurrentEmployee</c> policy is the
/// outer gate; this handler check is the inner gate that holds regardless of host or invocation path.
///
/// The hole: the handler trusted <c>Query.EmployeeId</c> verbatim, so any authenticated partner could
/// read another cleaner's order history by passing the victim's employee id. The fix mirrors the
/// reference siblings (<c>GetDashboardStats</c> / <c>GetEarningsAnalytics</c>):
///   - AC1: a NON-admin caller's foreign <c>EmployeeId</c> is IGNORED — the handler resolves the
///     caller's OWN id via <see cref="IOrderAccessService.GetCallerEmployeeIdAsync"/> and only that id
///     ever reaches the repository (the victim's data is never read);
///   - AC4: an Administrator caller's <c>Query.EmployeeId</c> IS honored (admin oversight);
///   - AC5: a non-admin with no resolvable employee gets the
///     <see cref="BusinessErrorMessage.EmployeeNotFound"/> business failure (no leak, no empty-200).
/// Written red → green per knowledge/testing.md (predates the handler fix).
/// </summary>
public class GetOrderAnalyticsHandlerTests
{
    private const string CallerEmployeeId = "emp-self-1";
    private const string ForeignEmployeeId = "emp-victim-2";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private readonly DateTime _start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _end = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    public GetOrderAnalyticsHandlerTests()
    {
        // The repository returns an empty set regardless of the id; the security assertion is about
        // WHICH id reaches the repository, not the rows it returns.
        _orderRepository
            .Setup(r => r.GetEmployeeOrdersByDateRangeAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());
    }

    private GetOrderAnalytics.Handler CreateHandler() =>
        (GetOrderAnalytics.Handler)Activator.CreateInstance(
            typeof(GetOrderAnalytics.Handler),
            _orderRepository.Object,
            _orderAccessService.Object,
            _session.Object)!;

    private void SetCaller(UserProfile role) =>
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));

    private GetOrderAnalytics.Query QueryFor(string? employeeId) =>
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
        // The victim's id must NEVER reach the repository...
        _orderRepository.Verify(r => r.GetEmployeeOrdersByDateRangeAsync(
            ForeignEmployeeId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // ...only the caller's own resolved id does.
        _orderRepository.Verify(r => r.GetEmployeeOrdersByDateRangeAsync(
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
        _orderRepository.Verify(r => r.GetEmployeeOrdersByDateRangeAsync(
            ForeignEmployeeId, _start, _end, It.IsAny<CancellationToken>()),
            Times.Once);
        // Admin oversight does not resolve the caller's own id.
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
        // No leak: the repository is never touched.
        _orderRepository.Verify(r => r.GetEmployeeOrdersByDateRangeAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
