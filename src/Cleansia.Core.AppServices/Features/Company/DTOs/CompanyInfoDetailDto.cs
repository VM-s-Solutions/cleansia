namespace Cleansia.Core.AppServices.Features.Company.DTOs;

public record CompanyInfoDetailDto(
    string Id,
    string LegalName,
    string TradingName,
    string? Tagline,
    string RegistrationNumber,
    string? VatNumber,
    string Street,
    string City,
    string ZipCode,
    string CountryId,
    string? CountryName,
    string? Phone,
    string? Email,
    string? Website,
    string? BankName,
    string? BankAccountNumber,
    string? Iban,
    string? Swift);