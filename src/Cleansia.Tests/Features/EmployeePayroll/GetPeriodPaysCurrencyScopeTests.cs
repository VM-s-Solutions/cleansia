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
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

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
        _currencyRepository
            .Setup(r => r.GetByIdAsync(EurId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CurrencyWithId(EurId, "EUR"));
        _currencyRepository
            .Setup(r => r.ExistsAsync(EurId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
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

    // ── the currency VIEW: a named currency is exact ─────────────────

    /// <summary>
    /// Both mobile apps open My Pay from an invoice and carry that invoice's currency to the screen; it
    /// is now sent to the server, and the server answers in it — whatever the cleaner resolves to.
    /// </summary>
    [Fact]
    public async Task A_Named_Currency_View_Overrides_The_Resolved_Currency()
    {
        ArrangeInvoices([]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 20m, currencyId: EurId),
            PayrollMockFactory.OrderPay(basePay: 25m, currencyId: EurId));

        var result = await CreateHandler().Handle(
            new GetPeriodPays.Query(EmployeeId, PayPeriodId, CurrencyId: EurId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal("EUR", dto.CurrencyCode);
        Assert.Equal(45m, dto.GrandTotal);
        Assert.Equal(2, dto.TotalOrders);
        Assert.All(dto.OrderPays, row => Assert.Equal("EUR", row.CurrencyCode));
    }

    /// <summary>
    /// A named view does NOT fall through to another currency's invoice. The fallback exists for the
    /// unnamed case (the cleaner holds a document in the other currency and the screen must agree with
    /// it); a caller who asked for EUR and got CZK would be looking at the wrong money.
    /// </summary>
    [Fact]
    public async Task A_Named_Currency_View_Does_Not_Fall_Through_To_Another_Currencys_Invoice()
    {
        ArrangeInvoices([InvoiceIn(PayrollMockFactory.CurrencyId, "CZK")]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 20m, currencyId: EurId));

        var result = await CreateHandler().Handle(
            new GetPeriodPays.Query(EmployeeId, PayPeriodId, CurrencyId: EurId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal("EUR", dto.CurrencyCode);
        Assert.Equal(20m, dto.GrandTotal);
        Assert.False(dto.HasInvoice);
        Assert.Null(dto.InvoiceId);
    }

    /// <summary>
    /// The per-row code is the summary's code on every row, never null: the DTO promises one currency
    /// for everything on it, and the row now says so itself so a client can label a row without
    /// reaching for the summary.
    /// </summary>
    [Fact]
    public async Task Every_Row_Names_The_Summarys_Currency()
    {
        ArrangeInvoices([]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 400m),
            PayrollMockFactory.OrderPay(basePay: 20m, currencyId: EurId));

        var result = await CreateHandler().Handle(new GetPeriodPays.Query(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(2, dto.OrderPays.Count());
        Assert.All(dto.OrderPays, row =>
        {
            Assert.NotNull(row.CurrencyCode);
            Assert.Equal(dto.CurrencyCode, row.CurrencyCode);
        });
    }

    [Fact]
    public async Task An_Unknown_Currency_View_Is_Refused()
    {
        _currencyRepository
            .Setup(r => r.ExistsAsync("currency-nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _employeeRepository.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payPeriodRepository.Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new GetPeriodPays.Validator(
            _employeeRepository.Object, _payPeriodRepository.Object, _currencyRepository.Object);

        var result = await validator.ValidateAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId, "currency-nope"));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.CurrencyNotFound, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task An_Unnamed_Currency_View_Is_Not_Existence_Checked()
    {
        _employeeRepository.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payPeriodRepository.Setup(r => r.ExistsAsync(PayPeriodId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new GetPeriodPays.Validator(
            _employeeRepository.Object, _payPeriodRepository.Object, _currencyRepository.Object);

        var result = await validator.ValidateAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId));

        Assert.True(result.IsValid);
        _currencyRepository.Verify(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
            _currencyResolution.Object,
            _currencyRepository.Object);

    private void ArrangeInvoices(IReadOnlyList<EmployeeInvoice> invoices) =>
        _invoiceRepository
            .Setup(r => r.GetAllForEmployeeAndPayPeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);

    // The repository includes the Currency navigation; the mocked rows carry it the same way.
    private void ArrangePays(params OrderEmployeePay[] pays)
    {
        foreach (var pay in pays)
        {
            var code = pay.CurrencyId == EurId ? "EUR" : "CZK";
            typeof(OrderEmployeePay).GetProperty(nameof(OrderEmployeePay.Currency))!
                .SetValue(pay, CurrencyWithId(pay.CurrencyId, code));
        }

        _orderPayRepository
            .Setup(r => r.GetByEmployeeAndPeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pays);
    }

    private static Currency CurrencyWithId(string id, string code)
    {
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        return currency;
    }

    private static EmployeeInvoice InvoiceIn(string currencyId, string code)
    {
        var invoice = PayrollMockFactory.Invoice(currencyId: currencyId, variableSymbol: PayrollMockFactory.NextTestVariableSymbol());
        var currency = Currency.Create(code, code, code);
        currency.Id = currencyId;
        typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.Currency))!.SetValue(invoice, currency);
        return invoice;
    }
}
