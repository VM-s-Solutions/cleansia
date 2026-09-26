using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Company;
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
using OrderService = Cleansia.Core.Domain.Orders.OrderService;

namespace Cleansia.Tests.Infrastructure.Pdf;

/// <summary>
/// THE LINES ARE NAMED IN THE DOCUMENT'S LANGUAGE, not only labelled in it.
///
/// <para>The labels around the lines were translated while each line still printed the catalogue's
/// base name, which the seed writes in English: a Czech receipt read "General Cleaning … Essential
/// Clean (balíček) … Inside oven cleaning", although every one of those rows carries its Czech name.
/// Asserted through <see cref="ReceiptService"/>, where the name is chosen — a fixture that handed the
/// layout a Czech name would prove nothing about where it comes from.</para>
/// </summary>
public class ReceiptItemNamesTests
{
    private const string CountryId = "cz";
    private const string CzechLanguageId = "lang-cs";
    private const string SlovakLanguageId = "lang-sk";

    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    private ReceiptPdfData? _rendered;

    public ReceiptItemNamesTests()
    {
        _pdfService
            .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Callback<ReceiptPdfData, string?>((data, _) => _rendered = data)
            .Returns([1, 2, 3]);
        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(Mock.Of<IBlobContainerClient>());
        _languageRepository
            .Setup(r => r.GetByIdAsync(CzechLanguageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Language.Create("cs", "Čeština"));
        _languageRepository
            .Setup(r => r.GetByIdAsync(SlovakLanguageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Language.Create("sk", "Slovenčina"));
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyInfo.Create("Cleansia s.r.o.", "Cleansia", "12345678", "Hlavní 1", "Praha", "11000", CountryId));
    }

    [Fact]
    public async Task A_Czech_Receipt_Names_Each_Line_From_The_Catalogues_Czech_Row()
    {
        var service = Service.Create("category-1", "General Cleaning", "Everything", 60)
            .SetTranslation("cs", "Obecný úklid", "Všechno");
        var package = Package.Create("Essential Clean", "The basics")
            .SetTranslation("cs", "Základní úklid", "Základ");
        var extra = Extra.Create("oven", "Inside oven cleaning", null)
            .SetTranslation("cs", "Čištění vnitřku trouby", null);

        var data = await RenderAsync(BookedOrder(service, package, extra), CzechLanguageId);

        Assert.Equal("Obecný úklid", Assert.Single(data.Services).Name);
        Assert.Equal("Základní úklid", Assert.Single(data.Packages).Name);
        Assert.Equal("Čištění vnitřku trouby", Assert.Single(data.Extras).Name);
    }

    /// <summary>
    /// A row with no name in the document's language is named in English where it has an English row,
    /// and by the base name it was created with where it has none.
    /// </summary>
    [Fact]
    public async Task A_Line_With_No_Name_In_The_Language_Falls_Back_To_English_Then_To_Its_Base_Name()
    {
        var service = Service.Create("category-1", "General Cleaning", "Everything", 60)
            .SetTranslation("cs", "Obecný úklid", "Všechno")
            .SetTranslation("en", "General cleaning", "Everything");
        var package = Package.Create("Essential Clean", "The basics");
        var extra = Extra.Create("oven", "Inside oven cleaning", null);

        var data = await RenderAsync(BookedOrder(service, package, extra), SlovakLanguageId);

        Assert.Equal("General cleaning", Assert.Single(data.Services).Name);
        Assert.Equal("Essential Clean", Assert.Single(data.Packages).Name);
        Assert.Equal("Inside oven cleaning", Assert.Single(data.Extras).Name);
    }

    private async Task<ReceiptPdfData> RenderAsync(Order order, string languageId)
    {
        await new ReceiptService(
                _pdfService.Object,
                Mock.Of<IOrderReceiptRepository>(),
                Mock.Of<IFiscalCounterRepository>(),
                _languageRepository.Object,
                _companyInfoRepository.Object,
                Mock.Of<ICountryRepository>(),
                Mock.Of<ICountryConfigurationRepository>(),
                _blobClientFactory.Object,
                Mock.Of<IFiscalServiceResolver>(),
                NullLogger<ReceiptService>.Instance)
            .RealizeFiscalAndPdfAsync(
                order,
                OrderReceipt.Create(order.Id, "2026-000001", "receipt.pdf", "2026/ORD/receipt.pdf", languageId),
                CancellationToken.None);

        return Assert.IsType<ReceiptPdfData>(_rendered);
    }

    private static Order BookedOrder(Service service, Package package, Extra extra)
    {
        var order = Order.Create(
            customerName: "Jan Novák",
            customerEmail: "jan@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Hlavní 2", "Praha", "11000", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1700m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending);
        order.Id = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
        order.AddSelectedServices([OrderService.Create(order, service, 1000m, 0m, 1000m)]);
        order.AddSelectedPackages([OrderPackage.Create(order, package, 500m)]);
        order.AddSelectedExtras([OrderExtra.Create(order, extra, 200m)]);
        return order;
    }
}
