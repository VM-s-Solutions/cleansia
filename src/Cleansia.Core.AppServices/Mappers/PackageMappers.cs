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
}