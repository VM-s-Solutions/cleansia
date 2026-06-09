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
/// ADR-0004 — the registered-but-stamp-not-persisted residual. The recovery path
/// (<see cref="ReceiptService.RetryFiscalRegistrationAsync"/>) re-calls the authority for the SAME
/// receipt; it must present the SAME idempotency token the initial register
/// (<see cref="ReceiptService.RealizeFiscalAndPdfAsync"/>) presented, so an idempotent provider
/// collapses the re-register onto the prior entry instead of burning a second one.
/// </summary>
public class ReceiptServiceFiscalIdempotencyTokenTests
{
    private const string CountryId = "de";
    private const string LanguageCode = "en";
    private const string ReceiptNumber = "RCP-2026-0042";

    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<IOrderReceiptRepository> _receiptRepository = new();
    private readonly Mock<IFiscalCounterRepository> _fiscalCounterRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IFiscalServiceResolver> _fiscalServiceResolver = new();
    private readonly RecordingIdempotentProvider _provider = new();

    public ReceiptServiceFiscalIdempotencyTokenTests()
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

        var country = Country.Create("Germany", "DE");
        _countryRepository
            .Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(country);

        var config = CountryConfiguration
            .Create(CountryId, "EUR", LanguageCode, standardVatRate: 19m)
            .UpdateFiscalEnforcementMode(FiscalEnforcementMode.BlockingOnline);
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_provider);

        _pdfService
            .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Returns([1, 2, 3]);

        var blobClient = new Mock<IBlobContainerClient>();
        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(blobClient.Object);
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

    private static Order BuildOrder()
    {
        var address = Address.Create("Hauptstr. 2", "Berlin", "10115", CountryId);
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+490000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "eur",
            paymentStatus: PaymentStatus.Pending);
        order.Id = "01HZX9N6M7Q8R9S0T1V2W3X4Y5";
        return order;
    }

    private static OrderReceipt BuildReceipt() =>
        OrderReceipt.Create("01HZX9N6M7Q8R9S0T1V2W3X4Y5", ReceiptNumber, "receipt.pdf", "2026/ORD/receipt.pdf", LanguageCode);

    [Fact]
    public async Task Initial_Register_Sends_ReceiptNumber_As_IdempotencyKey()
    {
        var order = BuildOrder();
        var receipt = BuildReceipt();

        await CreateService().RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, CancellationToken.None);

        Assert.Single(_provider.SeenKeys);
        Assert.Equal(ReceiptNumber, _provider.SeenKeys[0]);
    }

    [Fact]
    public async Task Recovery_ReRegister_Presents_Same_Token_So_Idempotent_Provider_Does_Not_Double_Register()
    {
        var order = BuildOrder();
        var receipt = BuildReceipt();
        var service = CreateService();

        await service.RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, CancellationToken.None);
        await service.RetryFiscalRegistrationAsync(receipt, order, CancellationToken.None);

        Assert.Equal(2, _provider.SeenKeys.Count);
        Assert.Equal(_provider.SeenKeys[0], _provider.SeenKeys[1]);
        Assert.Equal(1, _provider.AuthorityEntriesBurned);
    }

    private sealed class RecordingIdempotentProvider : IFiscalService
    {
        private readonly Dictionary<string, FiscalResult> _byKey = new(StringComparer.Ordinal);

        public string ProviderKey => "de-tse-test";
        public string CountryCode => "DE";
        public bool RegisterIsIdempotent => true;
        public List<string> SeenKeys { get; } = [];
        public int AuthorityEntriesBurned { get; private set; }

        public Task<FiscalResult> RegisterReceiptAsync(FiscalReceiptRequest request, CancellationToken cancellationToken)
        {
            SeenKeys.Add(request.IdempotencyKey);
            if (_byKey.TryGetValue(request.IdempotencyKey, out var prior))
            {
                return Task.FromResult(prior);
            }

            AuthorityEntriesBurned++;
            var result = FiscalResult.Success($"SIG-{request.IdempotencyKey}", request.IssuedAt.ToString("o"));
            _byKey[request.IdempotencyKey] = result;
            return Task.FromResult(result);
        }
    }
}
