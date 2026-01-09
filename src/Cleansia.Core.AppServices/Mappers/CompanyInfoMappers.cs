using Cleansia.Core.AppServices.Features.Company.DTOs;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Mappers;

public static class CompanyInfoMappers
{
    public static CompanyInfoDetailDto MapToDetailDto(this CompanyInfo companyInfo, Country? country = null) =>
        new(
            companyInfo.Id,
            companyInfo.LegalName,
            companyInfo.TradingName,
            companyInfo.Tagline,
            companyInfo.RegistrationNumber,
            companyInfo.VatNumber,
            companyInfo.Street,
            companyInfo.City,
            companyInfo.ZipCode,
            companyInfo.CountryId,
            country?.Name ?? companyInfo.Country?.Name,
            companyInfo.Phone,
            companyInfo.Email,
            companyInfo.Website,
            companyInfo.BankName,
            companyInfo.BankAccountNumber,
            companyInfo.Iban,
            companyInfo.Swift);

    public static CompanyInfoListItem MapToListItem(this CompanyInfo companyInfo) =>
        new(
            companyInfo.Id,
            companyInfo.LegalName,
            companyInfo.TradingName,
            companyInfo.CountryId,
            companyInfo.Country?.Name,
            companyInfo.City,
            companyInfo.Phone,
            companyInfo.Email,
            companyInfo.IsActive);
}