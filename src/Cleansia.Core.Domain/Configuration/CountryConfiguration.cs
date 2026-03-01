using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

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
    public string? DefaultPaymentGateway { get; private set; }

    [MaxLength(4000)]
    public string? LegalRequirementsJson { get; private set; }

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
        string? defaultPaymentGateway = null)
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
            DefaultPaymentGateway = defaultPaymentGateway
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
}
