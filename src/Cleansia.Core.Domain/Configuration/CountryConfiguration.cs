using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Fiscal.Abstractions;

namespace Cleansia.Core.Domain.Configuration;

public class CountryConfiguration : Auditable
{
    [Required]
    public string CountryId { get; private set; } = default!;

    public Country? Country { get; private set; }

    [Required]
    [MaxLength(3)]
    public string DefaultCurrencyCode { get; private set; } = default!;

    [Required]
    [MaxLength(10)]
    public string DefaultLanguageCode { get; private set; } = default!;

    [MaxLength(20)]
    public string? DateFormat { get; private set; }

    [MaxLength(50)]
    public string? TimeZoneId { get; private set; }

    [MaxLength(20)]
    public string? PhonePrefix { get; private set; }

    [Required]
    public decimal StandardVatRate { get; private set; }

    public decimal? ReducedVatRate { get; private set; }

    [MaxLength(50)]
    public string? TaxIdLabel { get; private set; }

    [MaxLength(100)]
    public string? TaxIdFormat { get; private set; }

    [MaxLength(50)]
    public string? RegistrationNumberLabel { get; private set; }

    [MaxLength(100)]
    public string? RegistrationNumberFormat { get; private set; }

    public bool RegistrationNumberRequired { get; private set; } = true;

    [MaxLength(50)]
    public string? VatNumberLabel { get; private set; }

    [MaxLength(100)]
    public string? VatNumberFormat { get; private set; }

    public bool VatNumberRequired { get; private set; }

    [MaxLength(50)]
    public string? DefaultPaymentGateway { get; private set; }

    [MaxLength(4000)]
    public string? LegalRequirementsJson { get; private set; }

    /// <summary>
    /// Per-country fiscal enforcement policy. Defaults to <see cref="FiscalEnforcementMode.None"/>
    /// for countries without a mandatory fiscal reporting system (e.g., CZ today).
    /// Strict countries (DE, AT, ES) use <see cref="FiscalEnforcementMode.BlockingOnline"/>
    /// so the receipt is held until the fiscal authority issues the signature.
    /// </summary>
    public FiscalEnforcementMode FiscalEnforcementMode { get; private set; } = FiscalEnforcementMode.None;

    public static CountryConfiguration Create(
        string countryId,
        string defaultCurrencyCode,
        string defaultLanguageCode,
        decimal standardVatRate,
        string? dateFormat = null,
        string? timeZoneId = null,
        string? phonePrefix = null,
        decimal? reducedVatRate = null,
        string? taxIdLabel = null,
        string? taxIdFormat = null,
        string? defaultPaymentGateway = null,
        string? registrationNumberLabel = null,
        string? registrationNumberFormat = null,
        bool registrationNumberRequired = true,
        string? vatNumberLabel = null,
        string? vatNumberFormat = null,
        bool vatNumberRequired = false)
        => new()
        {
            CountryId = countryId,
            DefaultCurrencyCode = defaultCurrencyCode,
            DefaultLanguageCode = defaultLanguageCode,
            StandardVatRate = standardVatRate,
            DateFormat = dateFormat,
            TimeZoneId = timeZoneId,
            PhonePrefix = phonePrefix,
            ReducedVatRate = reducedVatRate,
            TaxIdLabel = taxIdLabel,
            TaxIdFormat = taxIdFormat,
            DefaultPaymentGateway = defaultPaymentGateway,
            RegistrationNumberLabel = registrationNumberLabel,
            RegistrationNumberFormat = registrationNumberFormat,
            RegistrationNumberRequired = registrationNumberRequired,
            VatNumberLabel = vatNumberLabel,
            VatNumberFormat = vatNumberFormat,
            VatNumberRequired = vatNumberRequired
        };

    public CountryConfiguration UpdateVatRates(decimal standardRate, decimal? reducedRate)
    {
        StandardVatRate = standardRate;
        ReducedVatRate = reducedRate;
        return this;
    }

    public CountryConfiguration UpdateTaxIdSettings(string? label, string? format)
    {
        TaxIdLabel = label;
        TaxIdFormat = format;
        return this;
    }

    public CountryConfiguration UpdateBusinessIdentifierSettings(
        string? registrationNumberLabel,
        string? registrationNumberFormat,
        bool registrationNumberRequired,
        string? vatNumberLabel,
        string? vatNumberFormat,
        bool vatNumberRequired)
    {
        RegistrationNumberLabel = registrationNumberLabel;
        RegistrationNumberFormat = registrationNumberFormat;
        RegistrationNumberRequired = registrationNumberRequired;
        VatNumberLabel = vatNumberLabel;
        VatNumberFormat = vatNumberFormat;
        VatNumberRequired = vatNumberRequired;
        return this;
    }

    public CountryConfiguration UpdateDefaults(string currencyCode, string languageCode, string? dateFormat, string? timeZoneId)
    {
        DefaultCurrencyCode = currencyCode;
        DefaultLanguageCode = languageCode;
        DateFormat = dateFormat;
        TimeZoneId = timeZoneId;
        return this;
    }

    public CountryConfiguration UpdatePaymentGateway(string? gateway)
    {
        DefaultPaymentGateway = gateway;
        return this;
    }

    public CountryConfiguration UpdateLegalRequirements(string? legalRequirementsJson)
    {
        LegalRequirementsJson = legalRequirementsJson;
        return this;
    }

    public CountryConfiguration UpdateFiscalEnforcementMode(FiscalEnforcementMode mode)
    {
        FiscalEnforcementMode = mode;
        return this;
    }
}
