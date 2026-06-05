using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Dashboard;

/// <summary>
/// T-0104 (SEC-EMP-01) / ADR-0001 §D2 [OWN-DATA] — the inner ownership gate inside
/// <see cref="GetProductivityMetrics.Handler"/>, INCLUDING the <c>CalculatePersonalBestsAsync</c>
/// invoice path that leaks historical EARNINGS. Mirrors the sibling tests:
///   - AC3: a NON-admin caller's foreign <c>EmployeeId</c> is IGNORED — only the caller's own resolved
///     id reaches BOTH the order repository AND
///     <see cref="IEmployeeInvoiceRepository.GetByEmployeeAndDateRangeAsync"/> (no foreign historical
///     earnings / personal-best months);
///   - AC4: an Administrator caller's <c>Query.EmployeeId</c> IS honored on both paths;
///   - AC5: a non-admin with no resolvable employee gets
///     <see cref="BusinessErrorMessage.EmployeeNotFound"/> and neither repository is touched.
/// Written red → green per knowledge/testing.md (predates the handler fix).
/// </summary>
public class GetProductivityMetricsHandlerTests
{
    private const string CallerEmployeeId = "emp-self-1";
    private const string ForeignEmployeeId = "emp-victim-2";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public GetProductivityMetricsHandlerTests()
    {
        _orderRepository
            .Setup(r => r.GetCountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Order, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _orderRepository
            .Setup(r => r.GetCompletedOrdersByDateRangeAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Order>());
        _invoiceRepository
            .Setup(r => r.GetByEmployeeAndDateRangeAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EmployeeInvoice>());
    }

    private GetProductivityMetrics.Handler CreateHandler() =>
        (GetProductivityMetrics.Handler)Activator.CreateInstance(
            typeof(GetProductivityMetrics.Handler),
            _orderRepository.Object,
            _invoiceRepository.Object,
            _orderAccessService.Object,
            _session.Object)!;

    private void SetCaller(UserProfile role) =>
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));

    private GetProductivityMetrics.Query QueryFor(string? employeeId) =>
        new() { EmployeeId = employeeId };

    [Fact]
    public async Task NonAdmin_Supplying_Foreign_EmployeeId_Is_Scoped_To_Own_Id_Including_Earnings()
    {
        SetCaller(UserProfile.Employee);
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerEmployeeId);
        var handler = CreateHandler();

        var result = await handler.Handle(QueryFor(ForeignEmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The order path must be scoped to the caller's own id.
        _orderRepository.Verify(r => r.GetCompletedOrdersByDateRangeAsync(
            ForeignEmployeeId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // The EARNINGS / personal-bests path (CalculatePersonalBestsAsync) must ALSO use the resolved
        // id — never the foreign one. This is the historical-earnings leak the ticket calls out.
        _invoiceRepository.Verify(r => r.GetByEmployeeAndDateRangeAsync(
            ForeignEmployeeId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _invoiceRepository.Verify(r => r.GetByEmployeeAndDateRangeAsync(
            CallerEmployeeId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Admin_Supplying_EmployeeId_Is_Honored_On_Both_Paths()
    {
        SetCaller(UserProfile.Administrator);
        var handler = CreateHandler();

        var result = await handler.Handle(QueryFor(ForeignEmployeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _orderRepository.Verify(r => r.GetCompletedOrdersByDateRangeAsync(
            ForeignEmployeeId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _invoiceRepository.Verify(r => r.GetByEmployeeAndDateRangeAsync(
            ForeignEmployeeId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
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
        // No leak on either path.
        _orderRepository.Verify(r => r.GetCompletedOrdersByDateRangeAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _invoiceRepository.Verify(r => r.GetByEmployeeAndDateRangeAsync(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
