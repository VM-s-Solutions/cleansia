using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;

namespace Cleansia.Tests.Domain.Legal;

/// <summary>
/// A legal document is identified by its effective date (owner ruling, Q-AUD-L2): the version string a
/// consent row carries IS that date, the document is in force from that date, and the language a page
/// asks for falls back to English rather than to nothing.
/// </summary>
public sealed class LegalDocumentTests
{
    private static readonly DateOnly Effective = new(2026, 9, 14);

    [Fact]
    public void The_Version_Is_The_Effective_Date_As_Iso_Day()
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, null, Effective);

        Assert.Equal("2026-09-14", document.Version);
        Assert.Equal("2026-09-14", LegalDocument.VersionFor(Effective));
        Assert.InRange(document.Version.Length, 1, 32);
    }

    [Theory]
    [InlineData(2026, 9, 13, false)]
    [InlineData(2026, 9, 14, true)]
    [InlineData(2027, 1, 1, true)]
    public void A_Document_Is_In_Force_From_Its_Effective_Date_Inclusive(int year, int month, int day, bool expected)
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.PrivacyPolicy, null, Effective);

        Assert.Equal(expected, document.IsInForceOn(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(ConsentType.TermsOfService, LegalDocumentType.TermsOfService)]
    [InlineData(ConsentType.PrivacyPolicy, LegalDocumentType.PrivacyPolicy)]
    public void A_Legal_Consent_Names_Its_Document_Type(ConsentType consentType, LegalDocumentType expected)
    {
        Assert.Equal(expected, LegalDocument.TypeFor(consentType));
    }

    [Theory]
    [InlineData(ConsentType.MarketingEmails)]
    [InlineData(ConsentType.DataProcessing)]
    public void A_Consent_Without_A_Document_Names_None(ConsentType consentType)
    {
        Assert.Null(LegalDocument.TypeFor(consentType));
    }

    [Fact]
    public void A_Text_Hashes_Its_Markdown_And_Notices_A_Changed_Wording()
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, null, Effective);
        var text = document.AddText("EN", "Terms", "## One\n\nText.");

        Assert.Equal("en", text.Language);
        Assert.Equal(document.Id, text.LegalDocumentId);
        Assert.Equal(LegalDocumentText.HashOf("## One\n\nText."), text.ContentHash);
        Assert.Equal(64, text.ContentHash.Length);
        Assert.True(text.Matches("Terms", "## One\n\nText."));
        Assert.False(text.Matches("Terms", "## One\n\nText!"));
        Assert.False(text.Matches("Terms of Service", "## One\n\nText."));
    }

    [Fact]
    public void The_Requested_Language_Wins_By_Its_Primary_Subtag()
    {
        var document = Document("en", "cs");

        Assert.Equal("cs", document.TextForOrFallback("cs")!.Language);
        Assert.Equal("cs", document.TextForOrFallback("cs-CZ")!.Language);
        Assert.Equal("cs", document.TextForOrFallback("CS")!.Language);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("x")]
    public void An_Unknown_Or_Missing_Language_Falls_Back_To_English(string? language)
    {
        var document = Document("cs", "en");

        Assert.Equal("en", document.TextForOrFallback(language)!.Language);
    }

    [Fact]
    public void Without_English_The_First_Language_Serves()
    {
        var document = Document("uk", "cs");

        Assert.Equal("cs", document.TextForOrFallback("de")!.Language);
    }

    [Fact]
    public void A_Document_Without_Texts_Serves_Nothing()
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, null, Effective);

        Assert.Null(document.TextForOrFallback("en"));
    }

    private static LegalDocument Document(params string[] languages)
    {
        var document = LegalDocument.Create(LegalDocumentAudience.Customer, LegalDocumentType.TermsOfService, null, Effective);
        foreach (var language in languages)
        {
            document.AddText(language, $"Title {language}", $"Text {language}");
        }

        return document;
    }
}
