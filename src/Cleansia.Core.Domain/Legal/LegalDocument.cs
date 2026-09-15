using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Legal;

/// <summary>
/// One version of one legal text: the terms or the privacy policy, for one audience, for one market
/// or for the whole platform. Its identity is the effective date — <see cref="Version"/> is that date
/// as <c>yyyy-MM-dd</c>, the handle a consent row and an audit row carry — so every document the
/// platform ever showed stays on record under the date it started applying. Platform copy per market
/// like <c>CountryConfiguration</c>'s figures, hence tenantless.
///
/// <para>A document whose effective date has passed is immutable: what a customer accepted must read
/// the same forever. A change to the wording is a NEW document with a NEW effective date; the seeder
/// refuses to touch the texts of one already in force.</para>
/// </summary>
public class LegalDocument : BaseEntity
{
    public LegalDocumentAudience Audience { get; private set; }

    public LegalDocumentType Type { get; private set; }

    /// <summary>The market this copy is for; null is the platform-wide text a market without its own falls back to.</summary>
    [MaxLength(26)]
    public string? CountryId { get; private set; }

    public Country? Country { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    [Required]
    [MaxLength(32)]
    public string Version { get; private set; } = default!;

    [MaxLength(500)]
    public string? Notes { get; private set; }

    private readonly List<LegalDocumentText> _texts = [];
    public IReadOnlyCollection<LegalDocumentText> Texts => _texts.AsReadOnly();

    public static LegalDocument Create(
        LegalDocumentAudience audience,
        LegalDocumentType type,
        string? countryId,
        DateOnly effectiveFrom,
        string? notes = null)
        => new()
        {
            Audience = audience,
            Type = type,
            CountryId = countryId,
            EffectiveFrom = effectiveFrom,
            Version = VersionFor(effectiveFrom),
            Notes = notes
        };

    public static string VersionFor(DateOnly effectiveFrom) =>
        effectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The document a consent of this type records; null for the consents that have no text.</summary>
    public static LegalDocumentType? TypeFor(ConsentType consentType) => consentType switch
    {
        ConsentType.TermsOfService => LegalDocumentType.TermsOfService,
        ConsentType.PrivacyPolicy => LegalDocumentType.PrivacyPolicy,
        _ => null
    };

    public bool IsInForceOn(DateOnly today) => EffectiveFrom <= today;

    public LegalDocumentText AddText(string language, string title, string contentMarkdown)
    {
        var text = LegalDocumentText.Create(this, language, title, contentMarkdown);
        _texts.Add(text);
        return text;
    }

    public LegalDocumentText? TextFor(string language) =>
        _texts.FirstOrDefault(t => string.Equals(t.Language, language, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The requested language by its primary subtag ("cs-CZ" reads the Czech text), else English, else
    /// whatever the document has — a legal page in the wrong language beats no legal page.
    /// </summary>
    public LegalDocumentText? TextForOrFallback(string? language)
    {
        var primary = language is { Length: >= 2 } ? language[..2] : null;
        return (primary is null ? null : TextFor(primary))
            ?? TextFor(FallbackLanguage)
            ?? _texts.OrderBy(t => t.Language, StringComparer.Ordinal).FirstOrDefault();
    }

    public const string FallbackLanguage = "en";
}
