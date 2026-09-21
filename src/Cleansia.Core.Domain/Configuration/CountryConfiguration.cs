using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Tenancy;
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

    /// <summary>
    /// ADR-0034 D3 — which payout-identifier scheme banks in this country use. Null ⇒ the country is
    /// not open for payouts and only a self-describing (mod-97-valid) IBAN can be accepted.
    /// <para>Exactly one column, modelled on <see cref="FiscalEnforcementMode"/>. Deliberately not a
    /// <c>Label</c>/<c>Format</c>/<c>Required</c> triple like the tax-id fields: those describe one
    /// scalar whose variation is its name and regex, while a bank account is a structure whose field
    /// count changes per country — and no client reads the existing triples anyway.</para>
    /// </summary>
    public PayoutScheme? PayoutScheme { get; private set; }

    /// <summary>
    /// Per-country Stripe refund-fee rate as a percent (e.g. 1.4 means 1.4%). Null when no figure is
    /// pinned for the country yet — the partial-refund handler then treats the fee as 0 (the platform
    /// absorbs it; ADR-0009 D3). The fee <em>bearer</em> rule stays in <c>RefundPolicy</c>; this is only
    /// the fee <em>amount</em>.
    /// </summary>
    public decimal? RefundStripeFeeRate { get; private set; }

    /// <summary>
    /// Per-country fixed Stripe refund fee, a number in the country's <see cref="DefaultCurrencyCode"/>
    /// (6 means 6 CZK on the CZE row). Null → fee 0, same as <see cref="RefundStripeFeeRate"/>. Deducted
    /// only from a refund in that same currency; an order priced in any other currency has the fixed part
    /// absorbed by the platform (→ /product/business-rules#money-constants). No production writer sets
    /// either figure today — the seed leaves both null — so the fee is 0 everywhere until one does.
    /// </summary>
    public decimal? RefundStripeFixedFee { get; private set; }

    /// <summary>
    /// The insurance ceiling per booking that customer copy states for this country, a number in
    /// <see cref="DefaultCurrencyCode"/> (the <see cref="RefundStripeFixedFee"/> shape). Per country,
    /// not per currency: a policy is written per jurisdiction, so two EUR countries need not share one.
    /// Null → the clients render the copy variant that names no figure.
    /// </summary>
    public decimal? InsuranceCoverageAmount { get; private set; }

    /// <summary>
    /// The market a customer surface pre-selects before any choice is made (owner ruling
    /// 2026-09-13). At most one configuration carries it, held by a partial unique index the way the
    /// default currency is; <c>SetDefaultMarket</c> is the only writer. A pre-selection, not a pricing
    /// invariant: <c>GetMarkets</c> falls back to the default-currency rule when nothing is flagged.
    /// </summary>
    public bool IsDefaultMarket { get; private set; }

    /// <summary>
    /// The operating company that serves this market (ADR-0061 D2). Null means nobody does: GetMarkets
    /// does not list the country and an anonymous write naming it fails <c>tenant.not_found</c>.
    /// Seed-written; no admin writer until a second operator exists. Sits beside the future HomeRegion
    /// seam (ADR-0017 D2/D3).
    /// </summary>
    [MaxLength(26)]
    public string? OperatorTenantId { get; private set; }

    public Tenant? OperatorTenant { get; private set; }

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

    public CountryConfiguration UpdateRefundStripeFee(decimal? rate, decimal? fixedFee)
    {
        RefundStripeFeeRate = rate;
        RefundStripeFixedFee = fixedFee;
        return this;
    }

    public CountryConfiguration UpdateFiscalEnforcementMode(FiscalEnforcementMode mode)
    {
        FiscalEnforcementMode = mode;
        return this;
    }

    public CountryConfiguration UpdateMarketContent(decimal? insuranceCoverageAmount)
    {
        InsuranceCoverageAmount = insuranceCoverageAmount;
        return this;
    }

    public CountryConfiguration SetAsDefaultMarket(bool isDefaultMarket)
    {
        IsDefaultMarket = isDefaultMarket;
        return this;
    }

    public CountryConfiguration AssignOperator(string? tenantId)
    {
        OperatorTenantId = tenantId;
        return this;
    }
}
