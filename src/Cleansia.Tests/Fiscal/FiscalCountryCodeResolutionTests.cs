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
using Cleansia.Infra.Fiscal;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Fiscal;

/// <summary>
/// The two halves of the country code that decides which fiscal authority a receipt is declared to,
/// pinned against each other.
///
/// <para>They had drifted apart and nothing could see it: <c>Country.IsoCode</c> is stored ALPHA-3 by
/// the seed (<c>'CZE'</c>) and every <see cref="IFiscalService"/> declares ALPHA-2 (<c>"CZ"</c>), so
/// <see cref="FiscalServiceResolver"/> — which matches the two as strings — could never find a provider
/// for any market. Every receipt in every enforcement mode fell through to the no-op. The module is
/// disabled, so nothing failed; it would simply have registered nothing on the day it was switched on.
/// The existing fiscal fixtures all build their country rows with an alpha-2 code, which is why the
/// mismatch survived a whole suite.</para>
/// </summary>
public class FiscalCountryCodeResolutionTests
{
    private const string CzId = "cz";
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

    public FiscalCountryCodeResolutionTests()
    {
        _languageRepository
            .Setup(r => r.GetByCodeAsync(LanguageCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Language.Create(LanguageCode, "English"));

        var company = CompanyInfo.Create(
            legalName: "Cleansia CZ s.r.o.",
            tradingName: "Cleansia",
            registrationNumber: "12345678",
            street: "Hlavní 1",
            city: "Brno",
            zipCode: "60200",
            countryId: CzId);
        _companyInfoRepository
            .Setup(r => r.GetActiveByCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _companyInfoRepository
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CzId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration
                .Create(CzId, "CZK", LanguageCode, standardVatRate: 21m)
                .UpdateFiscalEnforcementMode(FiscalEnforcementMode.AsyncBackground));

        _pdfService
            .Setup(p => p.GenerateReceiptPdf(It.IsAny<ReceiptPdfData>(), It.IsAny<string?>()))
            .Returns([1, 2, 3]);

        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(new Mock<IBlobContainerClient>().Object);
    }

    /// <summary>
    /// The seed's own shape: the Czech country row stores <c>'CZE'</c>. The resolver must be asked for
    /// <c>"CZ"</c> — the code the Czech provider declares — or it answers the no-op and the receipt is
    /// never registered.
    /// </summary>
    [Fact]
    public async Task A_Country_Row_Stored_Alpha3_Resolves_The_Provider_By_Its_Alpha2_Code()
    {
        ArrangeCountry(storedIsoCode: "CZE");
        var provider = new RecordingFiscalService();
        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(provider);

        await CreateService().RealizeFiscalAndPdfAsync(
            BuildOrder(), BuildReceipt(), CancellationToken.None);

        _fiscalServiceResolver.Verify(r => r.Resolve("CZ"), Times.Once);
        Assert.Equal("CZ", provider.DeclaredCountryCodeOnRequest);
    }

    /// <summary>A row already stored alpha-2 keeps working — the reconciler passes it through.</summary>
    [Fact]
    public async Task A_Country_Row_Stored_Alpha2_Resolves_The_Same_Provider()
    {
        ArrangeCountry(storedIsoCode: "CZ");
        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(new RecordingFiscalService());

        await CreateService().RealizeFiscalAndPdfAsync(
            BuildOrder(), BuildReceipt(), CancellationToken.None);

        _fiscalServiceResolver.Verify(r => r.Resolve("CZ"), Times.Once);
    }

    /// <summary>
    /// A code the reconciler does not know resolves to the empty key, which is the no-op — never a
    /// guess at which authority the receipt belongs to.
    /// </summary>
    [Fact]
    public async Task An_Unknown_Country_Code_Falls_Through_To_The_Fallback_Not_To_A_Guess()
    {
        ArrangeCountry(storedIsoCode: "XXX");
        _fiscalServiceResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(new RecordingFiscalService());

        await CreateService().RealizeFiscalAndPdfAsync(
            BuildOrder(), BuildReceipt(), CancellationToken.None);

        _fiscalServiceResolver.Verify(r => r.Resolve(string.Empty), Times.Once);
    }

    /// <summary>
    /// The other half of the pair, and the one that stops a second country repeating the mistake: the
    /// resolver matches a provider's declared <see cref="IFiscalService.CountryCode"/> as a string, so
    /// every provider has to declare a code the country reconciler produces — an alpha-2 — or it is
    /// unreachable from any stored country row.
    /// </summary>
    [Theory]
    [MemberData(nameof(RegisteredFiscalProviders))]
    public void Every_Fiscal_Provider_Declares_A_Code_The_Country_Reconciler_Produces(
        string providerKey, string countryCode)
    {
        if (countryCode == NoOpCountryCode)
        {
            return;
        }

        Assert.Equal(
            countryCode,
            CountryIsoCode.ToAlpha2(countryCode));
        Assert.Equal(
            countryCode,
            CountryIsoCode.ToAlpha2(CountryIsoCode.ToAlpha3(countryCode)));
        Assert.False(string.IsNullOrWhiteSpace(providerKey));
    }

    private const string NoOpCountryCode = "*";

    /// <summary>
    /// Every fiscal provider the solution ships, read off the live implementations rather than a
    /// hand-typed mirror of them: a new country's service is picked up here the moment its type exists,
    /// enabled in configuration or not.
    /// </summary>
    public static TheoryData<string, string> RegisteredFiscalProviders()
    {
        var data = new TheoryData<string, string>();
        var implementations = typeof(FiscalServiceResolver).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IFiscalService).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var type in implementations)
        {
            // The declared code is a constant expression on every provider (it names a market, not a
            // configured value), so it is readable without the provider's dependencies.
            var instance = (IFiscalService)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(type);
            data.Add(instance.ProviderKey, instance.CountryCode);
        }

        return data;
    }

    private void ArrangeCountry(string storedIsoCode) =>
        _countryRepository
            .Setup(r => r.GetByIdAsync(CzId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Czechia", storedIsoCode, "CZ"));

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
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420777000111",
            customerAddress: Address.Create("Hlavní 2", "Brno", "60200", CzId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending);
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "czk";
        order.SetCurrency(czk);
        order.Id = "01HZX9N6M7Q8R9S0T1V2W3X4Y7";
        return order;
    }

    private static OrderReceipt BuildReceipt() =>
        OrderReceipt.Create("01HZX9N6M7Q8R9S0T1V2W3X4Y7", "2026-000009", "receipt.pdf", "2026/ORD/receipt.pdf", LanguageCode);

    /// <summary>Records the country code the receipt was actually declared under.</summary>
    private sealed class RecordingFiscalService : IFiscalService
    {
        public string ProviderKey => "cz-eet2-test";
        public string CountryCode => "CZ";
        public bool RegisterIsIdempotent => true;
        public string? DeclaredCountryCodeOnRequest { get; private set; }

        public Task<FiscalResult> RegisterReceiptAsync(FiscalReceiptRequest request, CancellationToken cancellationToken)
        {
            DeclaredCountryCodeOnRequest = request.CountryCode;
            return Task.FromResult(FiscalResult.Success("SIG-OK", DateTime.UtcNow.ToString("o")));
        }
    }
}
