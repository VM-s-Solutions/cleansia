using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// AC5 (T-0171c, AUD-04) — the read-only "my period pay" surface that stays on the Partner /
/// Mobile.Partner host. The settlement WRITE endpoints are off those hosts; the only payroll write
/// path a cleaner can reach is this read query, whose inner gate must resolve the EmployeeId FROM
/// SESSION (<see cref="IOrderAccessService.GetCallerEmployeeIdAsync"/>), never the request body
/// (same shape as SEC-EMP-01). This is the cross-user rejection proof (TC-AUTHZ harness).
/// Written red → green per knowledge/testing.md.
/// </summary>
public class GetPeriodPaysOwnershipTests
{
    private const string CallerEmployeeId = "emp-caller-1";
    private const string OtherEmployeeId = "emp-other-2";
    private const string PayPeriodId = "period-1";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private GetPeriodPays.Handler CreateHandler() =>
        new(
            _employeeRepository.Object,
            _payPeriodRepository.Object,
            _invoiceRepository.Object,
            _orderPayRepository.Object,
            _orderAccessService.Object,
            _session.Object);

    private void SetRole(UserProfile role) =>
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));

    private void ArrangeOwnPay()
    {
        _orderPayRepository
            .Setup(r => r.GetByEmployeeAndPeriodAsync(It.IsAny<string>(), PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPeriod.CreateBiWeekly(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public async Task Cleaner_Requesting_Another_Employees_Pay_Is_Rejected()
    {
        // The cleaner is authenticated as CallerEmployeeId (session) but forges OtherEmployeeId
        // into the query body. The handler must resolve the caller from session and reject.
        SetRole(UserProfile.Employee);
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerEmployeeId);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetPeriodPays.Query(OtherEmployeeId, PayPeriodId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.EmployeeNotFound, result.Error!.Message);
        _orderPayRepository.Verify(
            r => r.GetByEmployeeAndPeriodAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Cleaner_With_No_Resolvable_Session_Employee_Is_Rejected()
    {
        SetRole(UserProfile.Employee);
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetPeriodPays.Query(CallerEmployeeId, PayPeriodId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.EmployeeNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task Cleaner_Requesting_Own_Pay_Succeeds()
    {
        SetRole(UserProfile.Employee);
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerEmployeeId);
        ArrangeOwnPay();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetPeriodPays.Query(CallerEmployeeId, PayPeriodId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CallerEmployeeId, result.Value!.EmployeeId);
    }

    [Fact]
    public async Task Admin_Can_Read_Any_Employees_Pay()
    {
        SetRole(UserProfile.Administrator);
        ArrangeOwnPay();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new GetPeriodPays.Query(OtherEmployeeId, PayPeriodId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OtherEmployeeId, result.Value!.EmployeeId);
        // Admin path never consults the session-employee resolver.
        _orderAccessService.Verify(
            s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
