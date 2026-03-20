using Cleansia.Core.AppServices.Features.Packages.DTOs;

namespace Cleansia.Core.AppServices.Mappers;

public static class PackageMappers
{
    public static PackageListItem MapToDto(this Domain.Packages.Package package)
    {
        return new PackageListItem(
            Id: package.Id,
            Name: package.Name,
            Description: package.Description,
            Price: package.Price,
            Translations: package.Translations.ToDictionary(),
            IncludedServices: package.IncludedServices.Select(ps => new PackageServiceSummary(
                ps.Service.Name,
                ps.Service.Translations.ToDictionary())));
    }

    public static PackageDetails MapToDetails(this Domain.Packages.Package package, string currencyCode)
    {
        return new PackageDetails(
            Id: package.Id,
            Name: package.Name,
            Description: package.Description,
            Price: package.Price,
            EstimatedTime: package.IncludedServices.Sum(s => s.Service.EstimatedTime),
            CurrencyCode: currencyCode,
            IncludedServices: package.IncludedServices.Select(s => s.Service.Name)
        );
    }

    public static AdminPackageDetailDto MapToAdminDetail(this Domain.Packages.Package package)
    {
        return new AdminPackageDetailDto(
            Id: package.Id,
            Name: package.Name,
            Description: package.Description,
            Price: package.Price,
            Translations: package.Translations.ToDictionary(),
            IncludedServices: package.IncludedServices.Select(ps => new PackageServiceDto(
                ps.Service!.Id,
                ps.Service.Name,
                ps.Service.Description)),
            CreatedOn: package.CreatedOn,
            UpdatedOn: package.UpdatedOn);
    }
}