using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.InvoiceTemplates;

public class CountryInvoiceConfig : BaseEntity
{
    [Required]
    public string CountryId { get; private set; }
    public Country? Country { get; private set; }

    [Required]
    public bool VatRequired { get; private set; }

    [Required]
    public decimal VatRate { get; private set; }

    [Required]
    public bool DigitalSignatureRequired { get; private set; }

    [MaxLength(50)]
    public string? EInvoiceFormat { get; private set; }

    [MaxLength(2000)]
    public string? AdditionalFieldsJson { get; private set; }

    /// <summary>
    /// This jurisdiction's own legal notice, in the language it was written and reviewed in — not in
    /// the reader's. A notice reviewed for Czech law is a Czech-law artifact; translating it for
    /// readability produces a different artifact wearing the same authority, so the text follows the
    /// COUNTRY and only <see cref="LegalDisclaimerLanguageCode"/> records what language that is.
    /// </summary>
    [MaxLength(500)]
    public string? LegalDisclaimerTemplate { get; private set; }

    /// <summary>The language <see cref="LegalDisclaimerTemplate"/> is written in (ISO 639-1).</summary>
    [MaxLength(5)]
    public string? LegalDisclaimerLanguageCode { get; private set; }

    /// <summary>
    /// What stands behind <see cref="LegalDisclaimerTemplate"/>. Two countries can hold the same
    /// sentence and mean different things by it — one reviewed for that jurisdiction, one a copy of the
    /// generic fallback nobody looked at — so the assurance is a column and not an inference from the
    /// text. Below <see cref="LegalNoticeReviewStatus.BusinessSupplied"/> the text does not print.
    /// </summary>
    public LegalNoticeReviewStatus LegalDisclaimerReviewStatus { get; private set; } =
        LegalNoticeReviewStatus.NotReviewed;

    /// <summary>
    /// The konstantní symbol this country's payment system expects on an invoice of this kind — CZ uses
    /// <c>0308</c> for non-cash payments for goods and services. It is the PAYER's configuration, so it
    /// belongs to the platform's per-country invoice settings and never to a cleaner's bank record; a
    /// country whose payment system has no such code leaves it null and the field is omitted.
    /// </summary>
    [MaxLength(4)]
    public string? ConstantSymbol { get; private set; }

    public static CountryInvoiceConfig Create(
        string countryId,
        bool vatRequired,
        decimal vatRate,
        bool digitalSignatureRequired = false,
        string? eInvoiceFormat = null,
        string? additionalFieldsJson = null,
        string? legalDisclaimerTemplate = null,
        string? constantSymbol = null,
        string? legalDisclaimerLanguageCode = null,
        LegalNoticeReviewStatus legalDisclaimerReviewStatus = LegalNoticeReviewStatus.NotReviewed)
    {
        return new CountryInvoiceConfig
        {
            CountryId = countryId,
            VatRequired = vatRequired,
            VatRate = vatRate,
            DigitalSignatureRequired = digitalSignatureRequired,
            EInvoiceFormat = eInvoiceFormat,
            AdditionalFieldsJson = additionalFieldsJson,
            LegalDisclaimerTemplate = legalDisclaimerTemplate,
            ConstantSymbol = constantSymbol,
            LegalDisclaimerLanguageCode = legalDisclaimerLanguageCode,
            LegalDisclaimerReviewStatus = legalDisclaimerReviewStatus
        };
    }

    public CountryInvoiceConfig UpdateVatSettings(bool vatRequired, decimal vatRate)
    {
        VatRequired = vatRequired;
        VatRate = vatRate;
        return this;
    }

    public CountryInvoiceConfig UpdateDigitalSignature(bool required)
    {
        DigitalSignatureRequired = required;
        return this;
    }

    public CountryInvoiceConfig UpdateEInvoiceFormat(string? format)
    {
        EInvoiceFormat = format;
        return this;
    }

    /// <summary>
    /// The three move together: replacing the wording without restating its language and its assurance
    /// is how a reviewed notice quietly becomes an unreviewed one under a reviewed flag.
    /// </summary>
    public CountryInvoiceConfig UpdateLegalDisclaimer(
        string? disclaimer,
        string? languageCode,
        LegalNoticeReviewStatus reviewStatus)
    {
        LegalDisclaimerTemplate = disclaimer;
        LegalDisclaimerLanguageCode = languageCode;
        LegalDisclaimerReviewStatus = reviewStatus;
        return this;
    }

    public CountryInvoiceConfig UpdateAdditionalFields(string? fieldsJson)
    {
        AdditionalFieldsJson = fieldsJson;
        return this;
    }

    public CountryInvoiceConfig UpdateConstantSymbol(string? constantSymbol)
    {
        ConstantSymbol = constantSymbol;
        return this;
    }
}
