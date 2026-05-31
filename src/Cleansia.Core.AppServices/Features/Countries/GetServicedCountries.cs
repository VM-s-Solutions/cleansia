using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.Countries;

/// <summary>
/// Customer + partner-facing country list — only countries the company
/// actually operates in (Country.IsServiced && IsActive). Use this for any
/// picker the end user sees; <see cref="GetCountryOverview"/> is admin-only.
/// </summary>
public class GetServicedCountries
{
    public record Request : IRequest<IEnumerable<CountryListItem>>;

    public class Handler(ICountryRepository countryRepository) : IRequestHandler<Request, IEnumerable<CountryListItem>>
    {
        public async Task<IEnumerable<CountryListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var countries = await countryRepository.GetServicedAsync(cancellationToken);
            return countries.Select(c => c.MapToDto());
        }
    }
}
