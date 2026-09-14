using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Tests.Domain.Legal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The market a consent is stamped for: the one the request named, else the default market — the same
/// choice an anonymous request naming no market gets (ADR-0061 D3) — and the customer audience always.
/// No default market resolves the platform-wide text rather than nothing; no text at all is null, which
/// the callers record as "version unknown".
/// </summary>
public sealed class LegalDocumentResolverTests
{
    private const string Czechia = "country-cze";
    private const string Slovakia = "country-svk";

    private readonly Mock<ILegalDocumentRepository> _documents = new();
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly LegalDocument _terms = LegalDocumentFixtures.Terms();

    private LegalDocumentResolver Resolver() =>
        new(_documents.Object, _configurations.Object, NullLogger<LegalDocumentResolver>.Instance);

    private void DocumentFor(string? countryId) =>
        _documents
            .Setup(r => r.GetInForceAsync(LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, countryId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_terms);

    [Fact]
    public async Task A_Named_Market_Is_Passed_Through_Without_Consulting_The_Default()
    {
        DocumentFor(Slovakia);

        var document = await Resolver().ResolveInForceAsync(LegalDocumentType.TermsOfService, Slovakia, CancellationToken.None);

        Assert.Same(_terms, document);
        _configurations.Verify(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_Market_Named_Resolves_The_Default_Market()
    {
        _configurations
            .Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(Czechia, "CZK", "cs", 0.21m));
        DocumentFor(Czechia);

        var document = await Resolver().ResolveInForceAsync(LegalDocumentType.TermsOfService, null, CancellationToken.None);

        Assert.Same(_terms, document);
    }

    [Fact]
    public async Task No_Default_Market_Falls_Back_To_The_Platform_Wide_Text()
    {
        _configurations
            .Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryConfiguration?)null);
        DocumentFor(null);

        var document = await Resolver().ResolveInForceAsync(LegalDocumentType.TermsOfService, null, CancellationToken.None);

        Assert.Same(_terms, document);
    }

    [Fact]
    public async Task Today_Is_The_Utc_Day_And_The_Audience_Is_The_Customer()
    {
        DateOnly? asked = null;
        _documents
            .Setup(r => r.GetInForceAsync(LegalDocumentAudience.Customer, LegalDocumentType.PrivacyPolicy, Czechia, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Callback<LegalDocumentAudience, LegalDocumentType, string?, DateOnly, CancellationToken>((_, _, _, today, _) => asked = today)
            .ReturnsAsync((LegalDocument?)null);

        var before = DateOnly.FromDateTime(DateTime.UtcNow);
        var document = await Resolver().ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, Czechia, CancellationToken.None);
        var after = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Null(document);
        Assert.NotNull(asked);
        Assert.InRange(asked.Value, before, after);
    }
}
