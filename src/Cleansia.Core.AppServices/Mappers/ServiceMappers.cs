using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.AppServices.Mappers;

public static class ServiceMappers
{
    public static ServiceListItem MapToDto(this Service service)
    {
        return new ServiceListItem(
            Id: service.Id,
            Name: service.Name,
            Description: service.Description,
            BasePrice: service.BasePrice,
            PerRoomPrice: service.PerRoomPrice,
            Translations: service.Translations.ToDictionary());
    }

    public static ServiceDetails MapToDetails(this Service service, string currencyCode)
    {
        return new ServiceDetails(
            Id: service.Id,
            Name: service.Name,
            Description: service.Description,
            EstimatedTime: service.EstimatedTime,
            CurrencyCode: currencyCode);
    }

    public static AdminServiceDetailDto MapToAdminDetail(this Service service)
    {
        return new AdminServiceDetailDto(
            Id: service.Id,
            Name: service.Name,
            Description: service.Description,
            BasePrice: service.BasePrice,
            PerRoomPrice: service.PerRoomPrice,
            EstimatedTime: service.EstimatedTime,
            Translations: service.Translations.ToDictionary(),
            CreatedOn: service.CreatedOn,
            UpdatedOn: service.UpdatedOn);
    }
}