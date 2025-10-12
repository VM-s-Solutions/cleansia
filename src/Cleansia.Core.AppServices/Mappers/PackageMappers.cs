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
            Translations: package.Translations.ToDictionary());
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
}