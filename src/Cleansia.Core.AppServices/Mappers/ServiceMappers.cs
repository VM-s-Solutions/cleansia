using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.AppServices.Mappers;

public static class ServiceMappers
{
    /// <summary>
    /// <paramref name="basePrice"/> and <paramref name="perRoomPrice"/> are PASSED IN, not read off the
    /// entity, because a service no longer has one price — it has a price per currency, and only the
    /// caller knows which one this surface is answering for.
    ///
    /// <para>The rule the callers follow: a CATALOGUE surface passes the row for the currency being
    /// quoted; an ORDER surface passes the order's own frozen snapshot. The DTO field names do not
    /// move, and they never meant "the catalogue column" — they mean "the price in the currency you
    /// are being quoted in", which is exactly what a price row produces. Reading the live catalogue on
    /// an order surface is the defect the order-line snapshots exist to prevent.</para>
    /// </summary>
    public static ServiceListItem MapToDto(this Service service, decimal basePrice, decimal perRoomPrice)
    {
        return new ServiceListItem(
            Id: service.Id,
            Name: service.Name,
            Description: service.Description,
            Category: service.Category!.MapToDto(),
            BasePrice: basePrice,
            PerRoomPrice: perRoomPrice,
            Translations: service.Translations.ToDictionary());
    }

    public static CategoryDto MapToDto(this ServiceCategory category)
    {
        return new CategoryDto(
            Id: category.Id,
            Slug: category.Slug,
            Name: category.Name,
            Description: category.Description,
            DisplayOrder: category.DisplayOrder,
            Translations: category.Translations.ToDictionary());
    }

    public static ServiceDetails MapToDetails(this Service service, string currencyCode)
    {
        return new ServiceDetails(
            Id: service.Id,
            Name: service.Name,
            Description: service.Description,
            EstimatedTime: service.EstimatedTime,
            CurrencyCode: currencyCode,
            Translations: service.Translations.ToDictionary());
    }

    /// <summary>The admin detail shows the platform default currency's row. See MapToDto.</summary>
    public static AdminServiceDetailDto MapToAdminDetail(
        this Service service, decimal basePrice, decimal perRoomPrice)
    {
        return new AdminServiceDetailDto(
            Id: service.Id,
            Name: service.Name,
            Description: service.Description,
            CategoryId: service.CategoryId,
            BasePrice: basePrice,
            PerRoomPrice: perRoomPrice,
            EstimatedTime: service.EstimatedTime,
            Translations: service.Translations.ToDictionary(),
            CreatedOn: service.CreatedOn,
            UpdatedOn: service.UpdatedOn);
    }
}