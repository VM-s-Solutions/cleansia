using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Mappers;

public static class AddressMappers
{
    public static OrderAddress? MapToOrderAddress(this Address? address)
    {
        return address is null
            ? null
            : new OrderAddress(
                Street: address.Street,
                City: address.City,
                ZipCode: address.ZipCode,
                Country: address.Country?.Name!,
                Latitude: address.Latitude,
                Longitude: address.Longitude
            );
    }
}