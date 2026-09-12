using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Everything on PeriodPaySummaryDto is in the ONE currency it names -- the totals and the rows,
/// because the DTO promises "the currency every amount above is denominated in". The view is the
/// cleaner's resolved currency; when the period holds an invoice in it, that is the invoice shown;
/// when the period is invoiced only in another currency, the invoice's own currency wins, because the
/// payout document is what the cleaner holds. Pay in any other currency is not on this screen (T-0702).
/// </summary>
public class GetPeriodPaysCurrencyScopeTests
{
    private const string EmployeeId = "emp-1";
    private const string PayPeriodId = "period-1";
    private const string EurId = "currency-eur";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolution = new();

    public GetPeriodPaysCurrencyScopeTests()
    {
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString()));
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayrollMockFactory.OpenPeriod());

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = PayrollMockFactory.CurrencyId;
        _currencyResolution
            .Setup(s => s.ResolveCurrencyForEmployeeAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(czk);
    }

    [Fact]
    public async Task An_Un_Invoiced_Period_Sums_Only_The_Resolved_Currency()
    {
        ArrangeInvoices([]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 400m),
            PayrollMockFactory.OrderPay(basePay: 20m, currencyId: EurId));

        var result = await CreateHandler().Handle(new GetPeriodPays.Query(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(700m, dto.GrandTotal);
        Assert.Equal(700m, dto.TotalBasePay);
        Assert.Equal(2, dto.TotalOrders);
        Assert.Equal(2, dto.OrderPays.Count());
        Assert.Equal("CZK", dto.CurrencyCode);
        Assert.False(dto.HasInvoice);
    }

    [Fact]
    public async Task A_Period_Invoiced_Only_In_Another_Currency_Shows_That_Invoice_And_Its_Rows()
    {
        var eurInvoice = InvoiceIn(EurId, "EUR");
        ArrangeInvoices([eurInvoice]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 20m, currencyId: EurId));

        var result = await CreateHandler().Handle(new GetPeriodPays.Query(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(20m, dto.GrandTotal);
        Assert.Equal(1, dto.TotalOrders);
        Assert.Equal("EUR", dto.CurrencyCode);
        Assert.True(dto.HasInvoice);
        Assert.Equal(eurInvoice.Id, dto.InvoiceId);
    }

    /// <summary>
    /// One invoice per currency: when the pair holds both, the resolved currency's invoice is the one
    /// shown, and the choice is the same on every request.
    /// </summary>
    [Fact]
    public async Task A_Period_Invoiced_In_Both_Currencies_Shows_The_Resolved_Currencys_Invoice()
    {
        var czkInvoice = InvoiceIn(PayrollMockFactory.CurrencyId, "CZK");
        var eurInvoice = InvoiceIn(EurId, "EUR");
        ArrangeInvoices([czkInvoice, eurInvoice]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 20m, currencyId: EurId));

        var result = await CreateHandler().Handle(new GetPeriodPays.Query(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(300m, dto.GrandTotal);
        Assert.Equal("CZK", dto.CurrencyCode);
        Assert.Equal(czkInvoice.Id, dto.InvoiceId);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private GetPeriodPays.Handler CreateHandler() =>
        new(
            _employeeRepository.Object,
            _payPeriodRepository.Object,
            _invoiceRepository.Object,
            _orderPayRepository.Object,
            _orderAccessService.Object,
            _session.Object,
            _currencyResolution.Object);

    private void ArrangeInvoices(IReadOnlyList<EmployeeInvoice> invoices) =>
        _invoiceRepository
            .Setup(r => r.GetAllForEmployeeAndPayPeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);

    private void ArrangePays(params OrderEmployeePay[] pays) =>
        _orderPayRepository
            .Setup(r => r.GetByEmployeeAndPeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pays);

    private static EmployeeInvoice InvoiceIn(string currencyId, string code)
    {
        var invoice = PayrollMockFactory.Invoice(currencyId: currencyId, variableSymbol: PayrollMockFactory.NextTestVariableSymbol());
        var currency = Currency.Create(code, code, code);
        currency.Id = currencyId;
        typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.Currency))!.SetValue(invoice, currency);
        return invoice;
    }
}
