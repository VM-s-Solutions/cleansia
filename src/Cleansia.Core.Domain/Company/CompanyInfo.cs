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
