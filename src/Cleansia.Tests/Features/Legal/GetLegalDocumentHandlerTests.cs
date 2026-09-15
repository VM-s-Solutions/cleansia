using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Tests.Domain.Legal;
using FluentValidation.TestHelper;
using Moq;

namespace Cleansia.Tests.Features.Legal;

/// <summary>
/// The anonymous legal-page read: the market decides the currency the copy names and which document
/// is in force (a named market, else the default one); the language falls back to English; a market
/// that is not configured, or a market with no text yet, is a business refusal, never a 500.
/// </summary>
public sealed class GetLegalDocumentHandlerTests
{
    private const string Czechia = "country-cze";
    private const string Slovakia = "country-svk";

    private readonly Mock<ICountryConfigurationRepository> _configurations = new();
    private readonly Mock<ILegalDocumentResolver> _resolver = new();
    private readonly LegalDocument _terms;

    public GetLegalDocumentHandlerTests()
    {
        _terms = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, null, LegalDocumentFixtures.EffectiveFrom);
        _terms.AddText("en", "Terms of Service", "## Ordering & Payment\n\nPrices are displayed in {{currency}} and are final.");
        _terms.AddText("cs", "Obchodní podmínky", "## Objednávka a platba\n\nCeny jsou uvedeny v {{currency}} a jsou konečné.");

        _configurations
            .Setup(r => r.GetByCountryIdAsync(Czechia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(Czechia, "CZK", "cs", 0.21m));
        _configurations
            .Setup(r => r.GetByCountryIdAsync(Slovakia, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(Slovakia, "EUR", "sk", 0.20m));
        _configurations
            .Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(Czechia, "CZK", "cs", 0.21m));
        _resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_terms);
    }

    private GetLegalDocument.Handler Handler() => new(_configurations.Object, _resolver.Object);

    [Fact]
    public async Task The_Named_Markets_Currency_Fills_The_Copy_And_Its_Document_Is_Resolved()
    {
        var result = await Handler().Handle(new GetLegalDocument.Query(LegalDocumentType.TermsOfService, Slovakia, "en"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(LegalDocumentType.TermsOfService, dto.Type);
        Assert.Equal("2026-09-14", dto.Version);
        Assert.Equal(LegalDocumentFixtures.EffectiveFrom, dto.EffectiveFrom);
        Assert.Equal("en", dto.Language);
        Assert.Equal("Terms of Service", dto.Title);
        Assert.Contains("Prices are displayed in EUR and are final.", dto.ContentHtml);
        Assert.Contains("<h2>Ordering &amp; Payment</h2>", dto.ContentHtml);
        Assert.Equal(_terms.TextFor("en")!.ContentHash, dto.ContentHash);
        _resolver.Verify(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, Slovakia, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_Market_Named_Reads_The_Default_Market()
    {
        var result = await Handler().Handle(new GetLegalDocument.Query(LegalDocumentType.TermsOfService), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("in CZK and are final", result.Value.ContentHtml);
        _resolver.Verify(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, Czechia, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_Requested_Language_Is_Served_And_An_Unknown_One_Falls_Back_To_English()
    {
        var czech = await Handler().Handle(new GetLegalDocument.Query(LegalDocumentType.TermsOfService, Czechia, "cs"), CancellationToken.None);
        var german = await Handler().Handle(new GetLegalDocument.Query(LegalDocumentType.TermsOfService, Czechia, "de"), CancellationToken.None);

        Assert.Equal("cs", czech.Value.Language);
        Assert.Contains("Ceny jsou uvedeny v CZK", czech.Value.ContentHtml);
        Assert.Equal("en", german.Value.Language);
    }

    [Fact]
    public async Task A_Country_Without_A_Configuration_Is_Refused_As_Not_Serviced()
    {
        var result = await Handler().Handle(new GetLegalDocument.Query(LegalDocumentType.TermsOfService, "country-xxx"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CountryNotServiced, result.Error!.Message);
        _resolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task No_Default_Market_Is_Refused_As_Not_Serviced()
    {
        _configurations.Setup(r => r.GetDefaultMarketAsync(It.IsAny<CancellationToken>())).ReturnsAsync((CountryConfiguration?)null);

        var result = await Handler().Handle(new GetLegalDocument.Query(LegalDocumentType.TermsOfService), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.CountryNotServiced, result.Error!.Message);
    }

    [Fact]
    public async Task No_Document_In_Force_Is_Refused_As_Not_Found()
    {
        var result = await Handler().Handle(new GetLegalDocument.Query(LegalDocumentType.PrivacyPolicy, Czechia), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.LegalDocumentNotFound, result.Error!.Message);
    }

    [Fact]
    public void The_Validator_Refuses_An_Unknown_Type_And_Accepts_The_Optional_Market_And_Language()
    {
        var validator = new GetLegalDocument.Validator();

        validator.TestValidate(new GetLegalDocument.Query((LegalDocumentType)42))
            .ShouldHaveValidationErrorFor(q => q.Type).WithErrorMessage(BusinessErrorMessage.InvalidEnumValue);
        validator.TestValidate(new GetLegalDocument.Query(LegalDocumentType.PrivacyPolicy))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new GetLegalDocument.Query(LegalDocumentType.PrivacyPolicy, Czechia, "cs-CZ"))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new GetLegalDocument.Query(LegalDocumentType.PrivacyPolicy, new string('x', 27)))
            .ShouldHaveValidationErrorFor(q => q.CountryId).WithErrorMessage(BusinessErrorMessage.MaxLength);
    }
}
