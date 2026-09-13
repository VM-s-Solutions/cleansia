using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Legal;

/// <summary>
/// The version of each customer legal text in force, stamped on the consent row and the audit row at
/// the moment of acceptance (ADR-0062 D4). A legal-text edit bumps the constant and the
/// <c>terms_page.version</c> / <c>privacy_page.version</c> locale keys in the same change; the parity
/// checker pins the two together. The string is a dated draft handle today and becomes the
/// <c>AgreementVersion.Version</c> row handle when ADR-0041's tables land — nothing recorded now has to
/// be re-keyed.
/// </summary>
public static class LegalDocumentVersions
{
    public const string CustomerTerms = "2026-09-draft";

    public const string CustomerPrivacy = "2026-09-draft";

    public static string? For(ConsentType consentType) => consentType switch
    {
        ConsentType.TermsOfService => CustomerTerms,
        ConsentType.PrivacyPolicy => CustomerPrivacy,
        _ => null
    };
}
