using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Company;

public class CompanyInfo : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(200)]
    public string LegalName { get; private set; } = default!;

    [Required]
    [MaxLength(200)]
    public string TradingName { get; private set; } = default!;

    [MaxLength(500)]
    public string? Tagline { get; private set; }

    [Required]
    [MaxLength(50)]
    public string RegistrationNumber { get; private set; } = default!;

    [MaxLength(50)]
    public string? VatNumber { get; private set; }

    /// <summary>
    /// Indicates whether this company is registered as a VAT payer (plátce DPH in CZ).
    /// When false, receipts must display a "not a VAT payer" notice instead of VAT lines.
    /// When true, the VAT calculation uses the country's <c>StandardVatRate</c>.
    /// </summary>
    public bool IsVatPayer { get; private set; }

    /// <summary>
    /// The date VAT registration took effect, or null while the company is a neplátce.
    ///
    /// <para>Stored because it is UNRECOVERABLE after the fact and nothing else records it. The flag
    /// alone says what is true today; a receipt reprinted for an order supplied before registration has
    /// to know the company was not a payer THEN, and once the flag has flipped there is no way to work
    /// out when. It is also the date every VAT return is filed against.</para>
    ///
    /// <para><c>DateOnly</c>, not a timestamp: a tax authority registers a company on a DAY. Same
    /// choice as <c>PayPeriod.StartDate</c>.</para>
    /// </summary>
    public DateOnly? VatRegisteredFrom { get; private set; }

    [Required]
    [MaxLength(100)]
    public string Street { get; private set; } = default!;

    [Required]
    [MaxLength(100)]
    public string City { get; private set; } = default!;

    [Required]
    [MaxLength(20)]
    public string ZipCode { get; private set; } = default!;

    [Required]
    public string CountryId { get; private set; } = default!;

    public Country? Country { get; private set; }

    [MaxLength(50)]
    public string? Phone { get; private set; }

    [MaxLength(100)]
    public string? Email { get; private set; }

    [MaxLength(200)]
    public string? Website { get; private set; }

    [MaxLength(100)]
    public string? BankName { get; private set; }

    [MaxLength(50)]
    public string? BankAccountNumber { get; private set; }

    [MaxLength(50)]
    public string? Iban { get; private set; }

    [MaxLength(20)]
    public string? Swift { get; private set; }

    public static CompanyInfo Create(string legalName, string tradingName, string registrationNumber, string street, string city, string zipCode, string countryId, string? vatNumber = null, string? tagline = null, string? phone = null, string? email = null, string? website = null, string? bankName = null, string? bankAccountNumber = null, string? iban = null, string? swift = null)
    {
        return new CompanyInfo
        {
            LegalName = legalName,
            TradingName = tradingName,
            RegistrationNumber = registrationNumber,
            Street = street,
            City = city,
            ZipCode = zipCode,
            CountryId = countryId,
            VatNumber = vatNumber,
            Tagline = tagline,
            Phone = phone,
            Email = email,
            Website = website,
            BankName = bankName,
            BankAccountNumber = bankAccountNumber,
            Iban = iban,
            Swift = swift
        };
    }

    public CompanyInfo UpdateContactInfo(string? phone, string? email, string? website)
    {
        Phone = phone;
        Email = email;
        Website = website;
        return this;
    }

    public CompanyInfo UpdateBankDetails(string? bankName, string? bankAccountNumber, string? iban, string? swift)
    {
        BankName = bankName;
        BankAccountNumber = bankAccountNumber;
        Iban = iban;
        Swift = swift;
        return this;
    }

    public CompanyInfo UpdateTradingInfo(string tradingName, string? tagline)
    {
        TradingName = tradingName;
        Tagline = tagline;
        return this;
    }

    public CompanyInfo UpdateAddress(string street, string city, string zipCode, string countryId)
    {
        Street = street;
        City = city;
        ZipCode = zipCode;
        CountryId = countryId;
        return this;
    }

    public CompanyInfo UpdateTaxInfo(string registrationNumber, string? vatNumber)
    {
        RegistrationNumber = registrationNumber;
        VatNumber = vatNumber;
        return this;
    }

    /// <summary>
    /// Turns VAT on or off, and it is the ONLY way either happens.
    ///
    /// <para>Until now this had no production caller at all — its only references were its own
    /// definition and one unit test. <c>Create</c> did not take the flag and its initializer omitted it,
    /// so every company row was permanently <c>false</c>, and neither admin command carried it.
    /// Registering for VAT meant a hand-written UPDATE against the production database, which is the one
    /// operation the owner has forbidden outright.</para>
    ///
    /// <para><b>Clearing the flag clears the VAT number</b>, because a neplátce that keeps one is how
    /// §108 ZDPH liability gets stated on a document by accident. Callers therefore apply this AFTER
    /// <see cref="UpdateTaxInfo"/> — the entity is the arbiter, not the ordering, but the ordering is
    /// what lets the entity win.</para>
    ///
    /// <para>Clearing also clears the registration date: a company that is not registered has no date
    /// on which it became registered, and leaving a stale one behind would make the next flip look like
    /// a re-registration on the old date.</para>
    /// </summary>
    public CompanyInfo SetVatPayerStatus(bool isVatPayer, DateOnly? registeredFrom = null)
    {
        IsVatPayer = isVatPayer;
        if (!isVatPayer)
        {
            VatNumber = null;
            VatRegisteredFrom = null;
            return this;
        }

        VatRegisteredFrom = registeredFrom;
        return this;
    }

    public CompanyInfo UpdateLegalInfo(string legalName, string tradingName, string? tagline)
    {
        LegalName = legalName;
        TradingName = tradingName;
        Tagline = tagline;
        return this;
    }

    public string GetFullAddress()
    {
        return $"{Street}, {City} {ZipCode}";
    }

    public string GetFormattedContactInfo()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Phone)) parts.Add($"Tel: {Phone}");
        if (!string.IsNullOrWhiteSpace(Email)) parts.Add($"Email: {Email}");
        if (!string.IsNullOrWhiteSpace(Website)) parts.Add($"Web: {Website}");
        return string.Join(" | ", parts);
    }
}
