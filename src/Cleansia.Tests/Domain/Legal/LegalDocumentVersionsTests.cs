using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;

namespace Cleansia.Tests.Domain.Legal;

/// <summary>
/// ADR-0062 D4 — the two customer legal documents carry a dated version string; the consents that have
/// no document (marketing, data processing) carry none, so a consent row for them stays unversioned.
/// </summary>
public sealed class LegalDocumentVersionsTests
{
    [Theory]
    [InlineData(ConsentType.TermsOfService, LegalDocumentVersions.CustomerTerms)]
    [InlineData(ConsentType.PrivacyPolicy, LegalDocumentVersions.CustomerPrivacy)]
    public void A_Legal_Document_Resolves_To_Its_Version(ConsentType consentType, string expected)
    {
        Assert.Equal(expected, LegalDocumentVersions.For(consentType));
    }

    [Theory]
    [InlineData(ConsentType.MarketingEmails)]
    [InlineData(ConsentType.DataProcessing)]
    public void A_Consent_Without_A_Document_Has_No_Version(ConsentType consentType)
    {
        Assert.Null(LegalDocumentVersions.For(consentType));
    }

    [Fact]
    public void The_Versions_Fit_The_Column_And_Are_Not_Blank()
    {
        foreach (var version in new[] { LegalDocumentVersions.CustomerTerms, LegalDocumentVersions.CustomerPrivacy })
        {
            Assert.False(string.IsNullOrWhiteSpace(version));
            Assert.InRange(version.Length, 1, 32);
        }
    }
}
