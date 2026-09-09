using Cleansia.Core.AppServices.Features.Packages.DTOs;

namespace Cleansia.Core.AppServices.Mappers;

public static class PackageMappers
{
    /// <summary>
    /// <paramref name="price"/> is passed in rather than read off the entity — see
    /// <see cref="ServiceMappers"/> for the rule and why the DTO field name does not move.
    /// </summary>
    public static PackageListItem MapToDto(this Domain.Packages.Package package, decimal price)
    {
        return new PackageListItem(
            Id: package.Id,
            Name: package.Name,
            Description: package.Description,
            Tagline: package.Tagline,
            IsPopular: package.IsPopular,
            Price: price,
            Translations: package.Translations.ToDictionary(),
            IncludedServices: package.IncludedServices.Select(ps => new PackageServiceSummary(
                ps.ServiceId,
                ps.Service.Name,
                ps.Service.Translations.ToDictionary())));
    }

    public static PackageDetails MapToDetails(
        this Domain.Packages.Package package, string currencyCode, decimal price)
    {
        return new PackageDetails(
            Id: package.Id,
            Name: package.Name,
            Description: package.Description,
            Tagline: package.Tagline,
            IsPopular: package.IsPopular,
            Price: price,
            EstimatedTime: package.IncludedServices.Sum(s => s.Service.EstimatedTime),
            CurrencyCode: currencyCode,
            IncludedServices: package.IncludedServices.Select(s => s.Service.Name),
            IncludedServiceItems: package.IncludedServices.Select(s => new PackageServiceRef(s.Service.Id, s.Service.Name)),
            Translations: package.Translations.ToDictionary()
        );
    }

    /// <summary>The admin detail shows the platform default currency's row. See MapToDto.</summary>
    public static AdminPackageDetailDto MapToAdminDetail(this Domain.Packages.Package package, decimal price)
    {
        return new AdminPackageDetailDto(
            Id: package.Id,
            Name: package.Name,
            Description: package.Description,
            Tagline: package.Tagline,
            IsPopular: package.IsPopular,
            Price: price,
            Translations: package.Translations.ToDictionary(),
            IncludedServices: package.IncludedServices.Select(ps => new PackageServiceDto(
                ps.Service!.Id,
                ps.Service.Name,
                ps.Service.Description,
                ps.PriceWeight)),
            CreatedOn: package.CreatedOn,
            UpdatedOn: package.UpdatedOn);
    }
}