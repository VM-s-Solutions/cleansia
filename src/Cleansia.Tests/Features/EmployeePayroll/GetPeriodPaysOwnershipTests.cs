using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
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
    private readonly Mock<ICurrencyResolutionService> _currencyResolution = new();

    private GetPeriodPays.Handler CreateHandler() =>
        new(
            _employeeRepository.Object,
            _payPeriodRepository.Object,
            _invoiceRepository.Object,
            _orderPayRepository.Object,
            _orderAccessService.Object,
            _session.Object,
            _currencyResolution.Object);

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

    // ── Currency (MS-4) ──
    //
    // "My Pay" printed a hardcoded "Kč" until 2026-08-14. The day a second country configuration
    // exists that disagrees with the cleaner's own payout invoice — a document they file with their tax
    // return — so the DTO carries the currency and the two sources must not be able to drift.

    /// <summary>
    /// The invoice wins whenever there is one. Once a period is invoiced, that row IS what the payout
    /// document says; resolving independently could produce a different answer and reintroduce exactly
    /// the divergence this field closes.
    /// </summary>
    [Fact]
    public async Task An_Invoiced_Period_Reports_The_Invoices_Own_Currency()
    {
        SetRole(UserProfile.Administrator);
        ArrangeOwnPay();
        _invoiceRepository
            .Setup(r => r.GetByEmployeeAndPayPeriodAsync(CallerEmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(InvoiceWithCurrency("EUR"));
        _currencyResolution
            .Setup(s => s.ResolveCurrencyCodeForEmployeeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("CZK");

        var result = await CreateHandler().Handle(
            new GetPeriodPays.Query(CallerEmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("EUR", result.Value!.CurrencyCode);
    }

    /// <summary>An un-invoiced period has no stamped currency, so it resolves — the same way the
    /// partner dashboard does, so those two cannot drift either.</summary>
    [Fact]
    public async Task An_Un_Invoiced_Period_Resolves_From_The_Employees_Work_Country()
    {
        SetRole(UserProfile.Administrator);
        ArrangeOwnPay();
        _invoiceRepository
            .Setup(r => r.GetByEmployeeAndPayPeriodAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeInvoice?)null);
        _currencyResolution
            .Setup(s => s.ResolveCurrencyCodeForEmployeeAsync(CallerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("CZK");

        var result = await CreateHandler().Handle(
            new GetPeriodPays.Query(CallerEmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("CZK", result.Value!.CurrencyCode);
    }

    private static EmployeeInvoice InvoiceWithCurrency(string code)
    {
        var invoice = (EmployeeInvoice)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(EmployeeInvoice));
        typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.Currency))!
            .SetValue(invoice, CurrencyWithCode(code));
        return invoice;
    }

    private static Currency CurrencyWithCode(string code)
    {
        var currency = (Currency)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(Currency));
        typeof(Currency).GetProperty(nameof(Currency.Code))!.SetValue(currency, code);
        return currency;
    }
}
