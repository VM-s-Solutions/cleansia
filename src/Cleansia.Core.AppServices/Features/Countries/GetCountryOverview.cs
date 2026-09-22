using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Countries;

public class GetCountryOverview
{
    public record Request : IRequest<IEnumerable<CountryListItem>>;

    public class Handler(
        ICountryRepository countryRepository,
        ICountryConfigurationRepository countryConfigurationRepository) : IRequestHandler<Request, IEnumerable<CountryListItem>>
    {
        public async Task<IEnumerable<CountryListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var defaultMarketCountryId = (await countryConfigurationRepository.GetDefaultMarketAsync(cancellationToken))?.CountryId;

            // Customer-facing — hide countries the admin has deactivated.
            var countries = await countryRepository.GetAll()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return countries.Select(c => c.MapToDto(c.Id == defaultMarketCountryId));
        }
    }
}