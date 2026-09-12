using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Fiscal;

/// <summary>
/// A fiscal receipt is registered in the ORDER's currency, and an order that reaches the register
/// without its currency loaded is refused — never registered in a guessed one. The old fallback
/// declared a EUR sale to the authority as CZK; a tax declaration in the wrong unit is not a degraded
/// receipt, it is a false one. The refusal lands on the receipt row as a recorded failure, the same
/// place an unreachable authority lands, so nothing 500s and the retry job sees it.
/// </summary>
public class ReceiptServiceCurrencyFailClosedTests
{
    private const string DeId = "de";
    private const string LanguageCode = "en";
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";

    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<IOrderReceiptRepository> _receiptRepository = new();
    private readonly Mock<IFiscalCounterRepository> _fiscalCounterRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IFiscalServiceResolver> _fiscalServiceResolver = new();
    private readonly CapturingFiscalService _provider = new();
    private readonly List<ReceiptPdfData> _renderedPdfs = [];

    public ReceiptServiceCurrencyFailClosedTests()
    {
        var company = CompanyInfo.Create(
            legalName: "Cleansia GmbH",
            tradingName: "Cleansia",
            registrationNumber: "HRB-1",
            street: "Hauptstr. 1",
            city: "Berlin",
            zipCode: "10115",
            countryId: DeId);
        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        _countryRepository
            .Setup(r => r.GetByIdAsync(DeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Germany", "DE"));
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(DeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration
                .Create(DeId, "EUR", LanguageCode, standardVatRate: 19m)
                .UpdateFiscalEnforcementMode(FiscalEnforcementMode.AsyncBackground));

        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_provider);

        _pdfService
            .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Callback((ReceiptPdfData data, string? _) => _renderedPdfs.Add(data))
            .Returns([1, 2, 3]);
        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(new Mock<IBlobContainerClient>().Object);
    }

    private ReceiptService CreateService() => new(
        _pdfService.Object,
        _receiptRepository.Object,
        _fiscalCounterRepository.Object,
        _languageRepository.Object,
        _companyInfoRepository.Object,
        _countryRepository.Object,
        _countryConfigurationRepository.Object,
        _blobClientFactory.Object,
        _fiscalServiceResolver.Object,
        NullLogger<ReceiptService>.Instance);

    private static Order BuildOrder(Currency? currency)
    {
        var address = Address.Create("Hauptstr. 2", "Berlin", "10115", DeId);
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+490000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "eur",
            paymentStatus: PaymentStatus.Pending);
        order.Id = OrderId;
        if (currency is not null)
        {
            order.SetCurrency(currency);
        }
        return order;
    }

    private static Currency Euro()
    {
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = "eur";
        return eur;
    }

    private static OrderReceipt BuildReceipt() =>
        OrderReceipt.Create(OrderId, "2026-000001", "receipt.pdf", "2026/ORD/receipt.pdf", LanguageCode);

    [Fact]
    public async Task The_Register_Carries_The_Orders_Own_Currency()
    {
        var receipt = BuildReceipt();

        await CreateService().RealizeFiscalAndPdfAsync(BuildOrder(Euro()), receipt, LanguageCode, CancellationToken.None);

        Assert.Equal("EUR", Assert.Single(_provider.Seen).CurrencyCode);
        Assert.Equal("SIG-OK", receipt.FiscalCode);
    }

    [Fact]
    public async Task An_Order_Without_Its_Currency_Is_Recorded_Failed_And_Never_Registered()
    {
        var receipt = BuildReceipt();

        var ex = await Record.ExceptionAsync(() =>
            CreateService().RealizeFiscalAndPdfAsync(BuildOrder(currency: null), receipt, LanguageCode, CancellationToken.None));

        Assert.Null(ex);
        Assert.Empty(_provider.Seen);
        Assert.Null(receipt.FiscalCode);
        Assert.True(receipt.FiscalRegistrationFailed);
        Assert.Contains(OrderId, receipt.FiscalError);
    }

    [Fact]
    public async Task The_Receipt_Pdf_Carries_The_Orders_Own_Currency_Symbol()
    {
        await CreateService().RealizeFiscalAndPdfAsync(BuildOrder(Euro()), BuildReceipt(), LanguageCode, CancellationToken.None);

        Assert.Equal("€", Assert.Single(_renderedPdfs).Currency);
    }

    /// <summary>
    /// The PDF still renders on a fiscal refusal, and it renders the total with NO unit: a guessed "Kč"
    /// on a EUR order is a false receipt in the customer's hands.
    /// </summary>
    [Fact]
    public async Task The_Receipt_Pdf_For_An_Order_Without_Its_Currency_Carries_No_Unit()
    {
        await CreateService().RealizeFiscalAndPdfAsync(BuildOrder(currency: null), BuildReceipt(), LanguageCode, CancellationToken.None);

        var rendered = Assert.Single(_renderedPdfs);
        Assert.Equal(string.Empty, rendered.Currency);
        Assert.DoesNotContain("Kč", rendered.Currency, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Retry_Register_Carries_The_Orders_Own_Currency()
    {
        var receipt = BuildReceipt();

        var succeeded = await CreateService().RetryFiscalRegistrationAsync(receipt, BuildOrder(Euro()), CancellationToken.None);

        Assert.True(succeeded);
        Assert.Equal("EUR", Assert.Single(_provider.Seen).CurrencyCode);
    }

    [Fact]
    public async Task A_Retry_On_An_Order_Without_Its_Currency_Is_Recorded_As_A_Failed_Attempt()
    {
        var receipt = BuildReceipt();

        var succeeded = await CreateService().RetryFiscalRegistrationAsync(receipt, BuildOrder(currency: null), CancellationToken.None);

        Assert.False(succeeded);
        Assert.Empty(_provider.Seen);
        Assert.Equal(1, receipt.FiscalRetryCount);
        Assert.Contains(OrderId, receipt.FiscalError);
    }

    private sealed class CapturingFiscalService : IFiscalService
    {
        public List<FiscalReceiptRequest> Seen { get; } = [];
        public string ProviderKey => "de-tse-test";
        public string CountryCode => "DE";
        public bool RegisterIsIdempotent => true;

        public Task<FiscalResult> RegisterReceiptAsync(FiscalReceiptRequest request, CancellationToken cancellationToken)
        {
            Seen.Add(request);
            return Task.FromResult(FiscalResult.Success("SIG-OK", DateTime.UtcNow.ToString("o")));
        }
    }
}
