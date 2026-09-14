using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Legal;
using Moq;

namespace Cleansia.Tests.Domain.Legal;

/// <summary>The customer texts as the seed ships them: platform-wide, one effective date, English at least.</summary>
public static class LegalDocumentFixtures
{
    public static readonly DateOnly EffectiveFrom = new(2026, 9, 14);
    public static readonly DateOnly OlderEffectiveFrom = new(2025, 1, 1);

    public static LegalDocument Terms(DateOnly? effectiveFrom = null, string? countryId = null) =>
        Document(LegalDocumentType.TermsOfService, effectiveFrom ?? EffectiveFrom, countryId, "Terms of Service");

    public static LegalDocument Privacy(DateOnly? effectiveFrom = null, string? countryId = null) =>
        Document(LegalDocumentType.PrivacyPolicy, effectiveFrom ?? EffectiveFrom, countryId, "Privacy Policy");

    public static LegalDocument Document(LegalDocumentType type, DateOnly effectiveFrom, string? countryId, string title)
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Customer, type, countryId, effectiveFrom);
        document.AddText("en", title, $"## Section\n\nThe {title} text in force from {effectiveFrom:yyyy-MM-dd}.");
        return document;
    }

    /// <summary>A resolver that answers the same two documents for every market.</summary>
    public static Mock<ILegalDocumentResolver> Resolver(LegalDocument? terms, LegalDocument? privacy)
    {
        var resolver = new Mock<ILegalDocumentResolver>();
        resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(terms);
        resolver
            .Setup(r => r.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(privacy);
        return resolver;
    }

    public static Mock<ILegalDocumentResolver> Resolver() => Resolver(Terms(), Privacy());
}
