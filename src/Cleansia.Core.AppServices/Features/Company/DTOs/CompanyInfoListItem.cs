namespace Cleansia.Core.AppServices.Features.Company.DTOs;

public record CompanyInfoListItem(
    string Id,
    string LegalName,
    string TradingName,
    string CountryId,
    string? CountryName,
    string City,
    string? Phone,
    string? Email,
    bool IsActive);