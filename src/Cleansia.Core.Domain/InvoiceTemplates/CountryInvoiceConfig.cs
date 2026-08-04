using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
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

    [MaxLength(500)]
    public string? LegalDisclaimerTemplate { get; private set; }

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
        string? constantSymbol = null)
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
            ConstantSymbol = constantSymbol
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

    public CountryInvoiceConfig UpdateLegalDisclaimer(string? disclaimer)
    {
        LegalDisclaimerTemplate = disclaimer;
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
