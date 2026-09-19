using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The currencies a period can be VIEWED in come from its pay rows, not its invoices: the view
/// currency first, then every other currency a pay row of the employee and period is denominated in,
/// ordered by code. One entry means there is nothing to switch to. An open period with pay in two
/// currencies therefore offers both, and a cancelled invoice's currency is offered only when a live
/// pay row is in it.
/// </summary>
public class GetPeriodPaysAvailableCurrenciesTests
{
    private const string EmployeeId = "emp-1";
    private const string PayPeriodId = "period-1";
    private const string CzkId = PayrollMockFactory.CurrencyId;
    private const string EurId = "currency-eur";
    private const string UsdId = "currency-usd";

    private static readonly IReadOnlyDictionary<string, string> Codes = new Dictionary<string, string>
    {
        [CzkId] = "CZK",
        [EurId] = "EUR",
        [UsdId] = "USD",
    };

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolution = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

    public GetPeriodPaysAvailableCurrenciesTests()
    {
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString()));
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayrollMockFactory.OpenPeriod());
        _currencyResolution
            .Setup(s => s.ResolveCurrencyForEmployeeAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CurrencyWithId(CzkId));
        foreach (var id in Codes.Keys)
        {
            _currencyRepository
                .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CurrencyWithId(id));
        }
    }

    [Fact]
    public async Task Rows_In_Two_Currencies_Offer_The_View_First_Then_The_Other()
    {
        ArrangeInvoices([]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 400m),
            PayrollMockFactory.OrderPay(basePay: 20m, currencyId: EurId));

        var dto = await HandleAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId));

        AssertOffered(dto, [(CzkId, "CZK"), (EurId, "EUR")]);
        Assert.Equal("CZK", dto.CurrencyCode);
        Assert.Equal(2, dto.OrderPays.Count());
        Assert.All(dto.OrderPays, row => Assert.Equal("CZK", row.CurrencyCode));
    }

    [Fact]
    public async Task Rows_In_One_Currency_Offer_Only_The_View()
    {
        ArrangeInvoices([]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 400m));

        var dto = await HandleAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId));

        AssertOffered(dto, [(CzkId, "CZK")]);
    }

    [Fact]
    public async Task No_Rows_And_No_Invoice_Offer_The_Resolved_Currency()
    {
        ArrangeInvoices([]);
        ArrangePays();

        var dto = await HandleAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId));

        AssertOffered(dto, [(CzkId, "CZK")]);
        Assert.Empty(dto.OrderPays);
    }

    /// <summary>
    /// A deep link names a currency the period has no row in. The view is still first, so the switch
    /// always contains the value it shows, and the rows are honestly empty rather than another
    /// currency's.
    /// </summary>
    [Fact]
    public async Task A_Named_View_With_No_Rows_In_It_Is_Offered_First()
    {
        ArrangeInvoices([]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 400m));

        var dto = await HandleAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId, CurrencyId: EurId));

        AssertOffered(dto, [(EurId, "EUR"), (CzkId, "CZK")]);
        Assert.Equal("EUR", dto.CurrencyCode);
        Assert.Empty(dto.OrderPays);
    }

    [Fact]
    public async Task A_Cancelled_Invoices_Currency_With_No_Pay_Row_Is_Not_Offered()
    {
        ArrangeInvoices([InvoiceIn(CzkId), CancelledInvoiceIn(UsdId)]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 400m));

        var dto = await HandleAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId));

        AssertOffered(dto, [(CzkId, "CZK")]);
        Assert.Equal("CZK", dto.CurrencyCode);
    }

    [Fact]
    public async Task A_Named_View_Does_Not_Offer_A_Cancelled_Invoices_Currency_Without_A_Pay_Row()
    {
        ArrangeInvoices([CancelledInvoiceIn(UsdId)]);
        ArrangePays(PayrollMockFactory.OrderPay(basePay: 300m));

        var dto = await HandleAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId, CurrencyId: CzkId));

        AssertOffered(dto, [(CzkId, "CZK")]);
    }

    /// <summary>
    /// The invoiced-elsewhere fallback follows a document the cleaner holds; a cancelled invoice is
    /// not one, so when it is the period's only invoice the view stays the resolved currency, over
    /// its own rows, and the cancelled currency is not offered.
    /// </summary>
    [Fact]
    public async Task An_Unnamed_View_Does_Not_Follow_A_Cancelled_Invoice_In_Another_Currency()
    {
        ArrangeInvoices([CancelledInvoiceIn(UsdId)]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 300m),
            PayrollMockFactory.OrderPay(basePay: 400m));

        var dto = await HandleAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId));

        AssertOffered(dto, [(CzkId, "CZK")]);
        Assert.Equal("CZK", dto.CurrencyCode);
        Assert.Equal(2, dto.OrderPays.Count());
        Assert.All(dto.OrderPays, row => Assert.Equal("CZK", row.CurrencyCode));
        Assert.False(dto.HasInvoice);
        Assert.Null(dto.InvoiceId);
    }

    [Fact]
    public async Task The_Other_Currencies_Are_Distinct_And_Ordered_By_Code_After_The_View()
    {
        ArrangeInvoices([]);
        ArrangePays(
            PayrollMockFactory.OrderPay(basePay: 10m, currencyId: UsdId),
            PayrollMockFactory.OrderPay(basePay: 20m, currencyId: EurId),
            PayrollMockFactory.OrderPay(basePay: 30m, currencyId: UsdId),
            PayrollMockFactory.OrderPay(basePay: 40m),
            PayrollMockFactory.OrderPay(basePay: 50m, currencyId: EurId));

        var dto = await HandleAsync(new GetPeriodPays.Query(EmployeeId, PayPeriodId));

        AssertOffered(dto, [(CzkId, "CZK"), (EurId, "EUR"), (UsdId, "USD")]);
    }

    private async Task<PeriodPaySummaryDto> HandleAsync(GetPeriodPays.Query query)
    {
        var result = await CreateHandler().Handle(query, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static void AssertOffered(PeriodPaySummaryDto dto, (string Id, string Code)[] expected)
    {
        Assert.NotNull(dto.AvailableCurrencies);
        Assert.Equal(expected, dto.AvailableCurrencies.Select(c => (c.Id, c.Code)).ToArray());
    }

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
            typeof(OrderEmployeePay).GetProperty(nameof(OrderEmployeePay.Currency))!
                .SetValue(pay, CurrencyWithId(pay.CurrencyId));
        }

        _orderPayRepository
            .Setup(r => r.GetByEmployeeAndPeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pays);
    }

    private static Currency CurrencyWithId(string id)
    {
        var code = Codes[id];
        var currency = Currency.Create(code, code, code);
        currency.Id = id;
        return currency;
    }

    private static EmployeeInvoice InvoiceIn(string currencyId)
    {
        var invoice = PayrollMockFactory.Invoice(currencyId: currencyId, variableSymbol: PayrollMockFactory.NextTestVariableSymbol());
        typeof(EmployeeInvoice).GetProperty(nameof(EmployeeInvoice.Currency))!.SetValue(invoice, CurrencyWithId(currencyId));
        return invoice;
    }

    private static EmployeeInvoice CancelledInvoiceIn(string currencyId) =>
        InvoiceIn(currencyId).Cancel("re-issued", "admin-1");
}
