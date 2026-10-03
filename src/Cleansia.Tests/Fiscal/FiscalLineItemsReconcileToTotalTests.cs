using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Fiscal;

/// <summary>
/// The lines declared to a fiscal authority sum to the total declared beside them. Extras, the dirtiness
/// and express surcharges and the tier, membership and promo discounts are on the customer's receipt, so
/// they are lines of the fiscal request too — a request listing only the services and packages states a total
/// its own lines contradict.
/// </summary>
public class FiscalLineItemsReconcileToTotalTests
{
    private const string CountryId = "de";
    private const string LanguageCode = "en";
    private const string OrderId = "01HZX9N6M7Q8R9S0T1V2W3X4Z9";

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

    public FiscalLineItemsReconcileToTotalTests()
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
            countryId: CountryId);
        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        _countryRepository
            .Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Germany", "DEU", "DE"));

        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration
                .Create(CountryId, "EUR", LanguageCode, standardVatRate: 19m)
                .UpdateFiscalEnforcementMode(FiscalEnforcementMode.BlockingOnline));

        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_provider);

        _pdfService
            .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Returns([1, 2, 3]);

        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(new Mock<IBlobContainerClient>().Object);
    }

    // Lines 1200 + 800 + 150 + 100 = 2250; express at 20 % of the lines = 450; the discounts come off
    // the charged 2700. A larger promo replaces the tier and membership discounts, so it stands alone.
    [Theory]
    [InlineData(100, 170, 0, 2430)]
    [InlineData(0, 0, 300, 2400)]
    public async Task The_Fiscal_Lines_Sum_To_The_Order_Total(
        int tierDiscount, int membershipDiscount, int promoDiscount, int totalPrice)
    {
        var order = BuildOrder(tierDiscount, membershipDiscount, promoDiscount, totalPrice);

        await CreateService().RealizeFiscalAndPdfAsync(order, BuildReceipt(), CancellationToken.None);

        var request = _provider.LastRequest!;
        Assert.Equal(order.TotalPrice, request.TotalAmount);
        Assert.Equal(order.TotalPrice, request.LineItems.Sum(l => l.Quantity * l.UnitPrice));
    }

    [Fact]
    public async Task Extras_The_Surcharge_And_Each_Discount_Are_Their_Own_Lines()
    {
        var order = BuildOrder(tierDiscount: 100, membershipDiscount: 170, promoDiscount: 0, totalPrice: 2430);

        await CreateService().RealizeFiscalAndPdfAsync(order, BuildReceipt(), CancellationToken.None);

        var lines = _provider.LastRequest!.LineItems.ToDictionary(l => l.Description, l => l.UnitPrice);
        Assert.Equal(150m, lines["Deep oven clean"]);
        Assert.Equal(100m, lines["Inside the fridge"]);
        Assert.Equal(450m, lines["Express surcharge"]);
        Assert.Equal(-100m, lines["Loyalty discount"]);
        Assert.Equal(-170m, lines["Cleansia Plus discount"]);
        Assert.DoesNotContain("Promo code discount", lines.Keys);
        Assert.DoesNotContain("Dirtiness surcharge", lines.Keys);
    }

    // Heavy: 30 % of the 2250 lines = 675, raw 2925; express at 20 % of the raw = 585; a 300 promo
    // charged against the surcharged price = 360, so 2250 + 675 + 585 - 360 = 3150.
    [Fact]
    public async Task The_Dirtiness_Surcharge_Is_Its_Own_Line_And_The_Lines_Still_Sum_To_The_Total()
    {
        var order = BuildOrder(
            tierDiscount: 0, membershipDiscount: 0, promoDiscount: 360, totalPrice: 3150,
            dirtinessSurcharge: 675m, expressSurcharge: 585m);

        await CreateService().RealizeFiscalAndPdfAsync(order, BuildReceipt(), CancellationToken.None);

        var request = _provider.LastRequest!;
        Assert.Equal(675m, Assert.Single(request.LineItems, l => l.Description == "Dirtiness surcharge").UnitPrice);
        Assert.Equal(order.TotalPrice, request.TotalAmount);
        Assert.Equal(order.TotalPrice, request.LineItems.Sum(l => l.Quantity * l.UnitPrice));
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

    private static Order BuildOrder(
        int tierDiscount,
        int membershipDiscount,
        int promoDiscount,
        int totalPrice,
        decimal dirtinessSurcharge = 0m,
        decimal expressSurcharge = 450m)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+490000000000",
            customerAddress: Address.Create("Hauptstr. 2", "Berlin", "10115", CountryId),
            rooms: 3,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: "eur",
            paymentStatus: PaymentStatus.Paid,
            tierDiscountAmount: tierDiscount,
            promoDiscountAmount: promoDiscount,
            membershipDiscountAmount: membershipDiscount);
        order.Id = OrderId;
        var euro = Currency.Create("EUR", "€", "Euro");
        euro.Id = "eur";
        order.SetCurrency(euro);

        order.AddSelectedServices([
            OrderService.Create(order, Service.Create("category-1", "Standard cleaning", "Standard"), 800m, 200m, 1200m),
        ]);
        order.AddSelectedPackages([
            OrderPackage.Create(order, Package.Create("Kitchen package", "Kitchen"), 800m),
        ]);
        order.AddSelectedExtras([
            OrderExtra.Create(order, Extra.Create("oven", "Deep oven clean", null), 150m),
            OrderExtra.Create(order, Extra.Create("fridge", "Inside the fridge", null), 100m),
        ]);
        var level = dirtinessSurcharge > 0m ? DirtinessLevel.Heavy : DirtinessLevel.Normal;
        order.SetDirtinessSurcharge(level, dirtinessSurcharge, BookingPolicy.DirtinessSurchargeRate(level));
        order.SetExpressSurcharge(expressSurcharge);

        return order;
    }

    private static OrderReceipt BuildReceipt() =>
        OrderReceipt.Create(OrderId, "RCP-2026-0099", "receipt.pdf", "2026/ORD/receipt.pdf", LanguageCode);

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
