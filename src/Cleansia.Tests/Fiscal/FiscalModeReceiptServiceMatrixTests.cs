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
/// Fiscal-mode characterization MATRIX, the <see cref="ReceiptService"/> generation branch
/// (<see cref="ReceiptService.RealizeFiscalAndPdfAsync"/> → <c>HandleFiscalAsync</c>). Pins the current
/// behaviour of <see cref="FiscalEnforcementMode.None"/> /
/// <see cref="FiscalEnforcementMode.AsyncBackground"/> / <see cref="FiscalEnforcementMode.BlockingOnline"/>
/// crossed with the authority outcome (success / required-but-not-registered / throw):
///
/// <list type="bullet">
/// <item>None never resolves a fiscal service and never stamps a code.</item>
/// <item>AsyncBackground and BlockingOnline both register exactly once; a success stamps the code; a
/// failure marks the receipt failed, leaves <c>FiscalCode == null</c>, and never propagates — the
/// customer flow is never blocked on the authority.</item>
/// </list>
///
/// This is a read-only characterization net (no fiscal production code is modified); the named theory
/// cells let a future change to a single mode branch fail in isolation.
/// </summary>
public class FiscalModeReceiptServiceMatrixTests
{
    private const string CzId = "cz";
    private const string DeId = "de";
    private const string LanguageCode = "en";

    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<IOrderReceiptRepository> _receiptRepository = new();
    private readonly Mock<IFiscalCounterRepository> _fiscalCounterRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();
    private readonly Mock<IFiscalServiceResolver> _fiscalServiceResolver = new();

    public FiscalModeReceiptServiceMatrixTests()
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
            countryId: DeId,
            vatNumber: "DE123456789");
        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        _countryRepository
            .Setup(r => r.GetByIdAsync(CzId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Czechia", "CZ"));
        _countryRepository
            .Setup(r => r.GetByIdAsync(DeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Germany", "DE"));

        _pdfService
            .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
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

    private void WireCountryConfig(string countryId, FiscalEnforcementMode mode) =>
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(countryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration
                .Create(countryId, "EUR", LanguageCode, standardVatRate: 19m)
                .UpdateFiscalEnforcementMode(mode));

    private static Order BuildOrder(string countryId)
    {
        var address = Address.Create("Hauptstr. 2", "Berlin", "10115", countryId);
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

    private static Order BuildOrderWithNullCountry()
    {
        var address = Address.Create("Hauptstr. 2", "Berlin", "10115", null!);
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
        order.Id = "01HZX9N6M7Q8R9S0T1V2W3X4Y6";
        return order;
    }

    private static OrderReceipt BuildReceipt() =>
        OrderReceipt.Create("01HZX9N6M7Q8R9S0T1V2W3X4Y5", "2026-000001", "receipt.pdf", "2026/ORD/receipt.pdf", LanguageCode);

    // ── AC1 — None: no fiscal service resolved, no code, no failure marker ──

    [Fact]
    public async Task None_Skips_Fiscal_Resolution_Entirely_And_Leaves_FiscalCode_Null()
    {
        WireCountryConfig(CzId, FiscalEnforcementMode.None);
        var order = BuildOrder(CzId);
        var receipt = BuildReceipt();

        await CreateService().RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, CancellationToken.None);

        _fiscalServiceResolver.Verify(r => r.Resolve(It.IsAny<string>()), Times.Never);
        Assert.Null(receipt.FiscalCode);
        Assert.False(receipt.FiscalRegistrationFailed);
    }

    [Fact]
    public async Task No_CountryConfiguration_Row_Defaults_To_None_And_Skips_Fiscal()
    {
        // No GetByCountryIdAsync setup → repo returns null → ResolveEnforcementMode defaults to None.
        var order = BuildOrder(CzId);
        var receipt = BuildReceipt();

        await CreateService().RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, CancellationToken.None);

        _fiscalServiceResolver.Verify(r => r.Resolve(It.IsAny<string>()), Times.Never);
        Assert.Null(receipt.FiscalCode);
    }

    [Fact]
    public async Task Null_CountryId_Defaults_To_None_And_Skips_Fiscal()
    {
        var order = BuildOrderWithNullCountry();
        var receipt = BuildReceipt();

        await CreateService().RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, CancellationToken.None);

        _fiscalServiceResolver.Verify(r => r.Resolve(It.IsAny<string>()), Times.Never);
        Assert.Null(receipt.FiscalCode);
    }

    // ── AC2 — registering modes, authority SUCCESS: register once, stamp the code ──

    [Theory]
    [InlineData(FiscalEnforcementMode.AsyncBackground)]
    [InlineData(FiscalEnforcementMode.BlockingOnline)]
    [InlineData(FiscalEnforcementMode.BlockingWithOfflineCache)]
    public async Task Registering_Mode_On_Success_Registers_Once_And_Stamps_FiscalData(FiscalEnforcementMode mode)
    {
        WireCountryConfig(DeId, mode);
        var provider = new ScriptedFiscalService(
            FiscalResult.Success("SIG-OK", new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc).ToString("o")));
        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(provider);

        var order = BuildOrder(DeId);
        var receipt = BuildReceipt();

        await CreateService().RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, CancellationToken.None);

        Assert.Equal(1, provider.RegisterCallCount);
        Assert.Equal("SIG-OK", receipt.FiscalCode);
        Assert.Equal(provider.ProviderKey, receipt.FiscalProviderKey);
        Assert.NotNull(receipt.FiscalRegisteredAt);
        Assert.False(receipt.FiscalRegistrationFailed);
    }

    // ── AC3/AC4 — registering modes, authority NOT-REGISTERED: mark failed, no code, no throw ──

    [Theory]
    [InlineData(FiscalEnforcementMode.AsyncBackground)]
    [InlineData(FiscalEnforcementMode.BlockingOnline)]
    [InlineData(FiscalEnforcementMode.BlockingWithOfflineCache)]
    public async Task Registering_Mode_On_RequiredButNotRegistered_Marks_Failed_And_Does_Not_Throw(FiscalEnforcementMode mode)
    {
        WireCountryConfig(DeId, mode);
        var provider = new ScriptedFiscalService(
            FiscalResult.TransientError("503", "authority unreachable"));
        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(provider);

        var order = BuildOrder(DeId);
        var receipt = BuildReceipt();

        var ex = await Record.ExceptionAsync(() =>
            CreateService().RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, CancellationToken.None));

        Assert.Null(ex);
        Assert.Equal(1, provider.RegisterCallCount);
        Assert.Null(receipt.FiscalCode);
        Assert.True(receipt.FiscalRegistrationFailed);
        Assert.Equal(FiscalErrorKind.Transient, receipt.FiscalErrorKind);
    }

    // ── AC3/AC4 — registering modes, authority THROWS: mark failed, no code, swallowed ──

    [Theory]
    [InlineData(FiscalEnforcementMode.AsyncBackground)]
    [InlineData(FiscalEnforcementMode.BlockingOnline)]
    [InlineData(FiscalEnforcementMode.BlockingWithOfflineCache)]
    public async Task Registering_Mode_When_Authority_Throws_Marks_Failed_And_Swallows(FiscalEnforcementMode mode)
    {
        WireCountryConfig(DeId, mode);
        var provider = new ScriptedFiscalService(new InvalidOperationException("connection reset"));
        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(provider);

        var order = BuildOrder(DeId);
        var receipt = BuildReceipt();

        var ex = await Record.ExceptionAsync(() =>
            CreateService().RealizeFiscalAndPdfAsync(order, receipt, LanguageCode, CancellationToken.None));

        Assert.Null(ex);
        Assert.Equal(1, provider.RegisterCallCount);
        Assert.Null(receipt.FiscalCode);
        Assert.True(receipt.FiscalRegistrationFailed);
        Assert.Equal(FiscalErrorKind.Unknown, receipt.FiscalErrorKind);
    }

    /// <summary>
    /// A scripted <see cref="IFiscalService"/> that returns a fixed <see cref="FiscalResult"/> (or throws
    /// a fixed exception) on <see cref="RegisterReceiptAsync"/>, counting calls. Declares
    /// <see cref="RegisterIsIdempotent"/> = true so it passes the <c>FiscalGoLiveGate</c> under blocking
    /// modes (a non-idempotent provider under a blocking mode is rejected at the seam, a separate contract).
    /// </summary>
    private sealed class ScriptedFiscalService : IFiscalService
    {
        private readonly FiscalResult? _result;
        private readonly Exception? _throw;

        public ScriptedFiscalService(FiscalResult result) => _result = result;
        public ScriptedFiscalService(Exception toThrow) => _throw = toThrow;

        public string ProviderKey => "de-tse-test";
        public string CountryCode => "DE";
        public bool RegisterIsIdempotent => true;
        public int RegisterCallCount { get; private set; }

        public Task<FiscalResult> RegisterReceiptAsync(FiscalReceiptRequest request, CancellationToken cancellationToken)
        {
            RegisterCallCount++;
            if (_throw is not null)
            {
                throw _throw;
            }

            return Task.FromResult(_result!);
        }
    }
}
