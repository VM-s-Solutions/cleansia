using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Countries;

public class GetServiceAreaOverview
{
    public record Request : IRequest<IEnumerable<ServiceAreaCountryDto>>;

    public class Handler(
        ICountryRepository countryRepository,
        ICountryConfigurationRepository countryConfigurationRepository) : IRequestHandler<Request, IEnumerable<ServiceAreaCountryDto>>
    {
        public async Task<IEnumerable<ServiceAreaCountryDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var countries = await countryRepository.GetAll()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            var countryIds = countries.Select(c => c.Id).ToList();
            var configurations = await countryConfigurationRepository.GetAll()
                .Where(c => countryIds.Contains(c.CountryId))
                .ToDictionaryAsync(c => c.CountryId, cancellationToken);

            return countries.Select(c => c.MapToServiceAreaDto(configurations.GetValueOrDefault(c.Id)));
        }
    }
}
