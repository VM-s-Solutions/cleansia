using Cleansia.Core.AppServices.Features.ServiceAreas.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.ServiceAreas;

/// <summary>
/// Lists serviced cities. Customer flows pass <see cref="Request.CountryId"/>
/// scoped to the address-picker's country; admin can leave it null to get
/// every active city across every country.
/// </summary>
public class GetServiceCities
{
    public record Request(string? CountryId = null) : IRequest<IEnumerable<ServiceCityDto>>;

    public class Handler(IServiceCityRepository cityRepository) : IRequestHandler<Request, IEnumerable<ServiceCityDto>>
    {
        public async Task<IEnumerable<ServiceCityDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var cities = string.IsNullOrEmpty(request.CountryId)
                ? await cityRepository.GetAllActiveAsync(cancellationToken)
                : await cityRepository.GetByCountryAsync(request.CountryId, cancellationToken);

            return cities.Select(c => c.MapToDto());
        }
    }
}
