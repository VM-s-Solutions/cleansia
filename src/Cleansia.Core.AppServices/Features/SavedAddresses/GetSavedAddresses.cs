using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.SavedAddresses;

public class GetSavedAddresses
{
    public record Query(string UserId) : IQuery<IReadOnlyList<SavedAddressDto>>;

    public class Handler(ISavedAddressRepository savedAddressRepository)
        : IQueryHandler<Query, IReadOnlyList<SavedAddressDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<SavedAddressDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var items = await savedAddressRepository.GetByUserAsync(query.UserId, cancellationToken);
            var dtos = items
                .Where(s => s.Address != null)
                .Select(s => new SavedAddressDto(
                    Id: s.Id,
                    Label: s.Label,
                    Street: s.Address!.Street,
                    City: s.Address.City,
                    ZipCode: s.Address.ZipCode,
                    State: s.Address.State,
                    CountryId: s.Address.CountryId,
                    Country: s.Address.Country?.Name,
                    Latitude: s.Address.Latitude,
                    Longitude: s.Address.Longitude,
                    IsDefault: s.IsDefault))
                .ToList();

            return BusinessResult.Success<IReadOnlyList<SavedAddressDto>>(dtos);
        }
    }
}
