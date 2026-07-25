using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Enums;
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
/// A card booking the cleaner settled in cash (its Stripe webhook never arrived) is legally a CASH
/// sale. <see cref="Order.PaymentType"/> stays Card — it is the booking contract the refund path keys
/// off — so the tender that reaches the fiscal authority and the receipt's payment label is the derived
/// <see cref="Order.ActualPaymentType"/>. Registering such a sale as a card payment misstates the
/// takings to the authority, which is exactly the class of error a fiscal regime exists to catch.
/// </summary>
public class ActualTenderRoutingTests
{
    private const string CountryId = "de";
    private const string LanguageCode = "en";
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
    private const string ReceiptNumber = "RCP-2026-0043";

    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<IOrderReceiptRepository> _receiptRepository = new();
    private readonly Mock<IFiscalCounterRepository> _fiscalCounterRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IFiscalServiceResolver> _fiscalServiceResolver = new();
    private readonly RecordingFiscalProvider _provider = new();

    private ReceiptPdfData? _pdfData;

    public ActualTenderRoutingTests()
    {
        _languageRepository
            .Setup(r => r.GetByCodeAsync(LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Language.Create(LanguageCode, "English"));

        var company = CompanyInfo.Create(
            legalName: "Cleansia GmbH",
            tradingName: "Cleansia",
            registrationNumber: "HRB-1",
            street: "Hauptstr. 1",
            city: "Berlin",
            zipCode: "10115",
            countryId: CountryId,
            vatNumber: "DE123456789");
        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        _countryRepository
            .Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Germany", "DE"));

        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration
                .Create(CountryId, "EUR", LanguageCode, standardVatRate: 19m)
                .UpdateFiscalEnforcementMode(FiscalEnforcementMode.BlockingOnline));

        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_provider);

        _pdfService
            .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Callback<ReceiptPdfData, string?>((data, _) => _pdfData = data)
            .Returns([1, 2, 3]);

        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(new Mock<IBlobContainerClient>().Object);
    }

    [Theory]
    [InlineData(PaymentType.Card, false, "Card")]
    [InlineData(PaymentType.Card, true, "Cash")]
    [InlineData(PaymentType.Cash, true, "Cash")]
    public async Task Fiscal_Registration_And_Receipt_Label_Carry_The_Tender_Actually_Taken(
        PaymentType bookedType, bool collectedInCash, string expectedTender)
    {
        var order = BuildOrder(bookedType, collectedInCash);

        await CreateService().RealizeFiscalAndPdfAsync(
            order, BuildReceipt(), LanguageCode, CancellationToken.None);

        Assert.Equal(expectedTender, _provider.LastRequest!.PaymentMethod);
        Assert.Equal(expectedTender, _pdfData!.PaymentType);
        Assert.Equal(bookedType, order.PaymentType);
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

    private static Order BuildOrder(PaymentType paymentType, bool collectedInCash)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+490000000000",
            customerAddress: Address.Create("Hauptstr. 2", "Berlin", "10115", CountryId),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "eur",
            paymentStatus: PaymentStatus.Pending);
        order.Id = OrderId;

        if (collectedInCash)
        {
            order.MarkCashCollected("emp-1");
        }

        return order;
    }

    private static OrderReceipt BuildReceipt() =>
        OrderReceipt.Create(OrderId, ReceiptNumber, "receipt.pdf", "2026/ORD/receipt.pdf", LanguageCode);

    private sealed class RecordingFiscalProvider : IFiscalService
    {
        public string ProviderKey => "de-tse-test";
        public string CountryCode => "DE";
        public bool RegisterIsIdempotent => true;
        public FiscalReceiptRequest? LastRequest { get; private set; }

        public Task<FiscalResult> RegisterReceiptAsync(FiscalReceiptRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(FiscalResult.Success($"SIG-{request.IdempotencyKey}", request.IssuedAt.ToString("o")));
        }
    }
}
