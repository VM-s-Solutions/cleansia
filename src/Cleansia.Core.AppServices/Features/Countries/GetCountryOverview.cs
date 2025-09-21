using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Countries;

public class GetCountryOverview
{
    public record Request : IRequest<IEnumerable<CountryListItem>>;

    public class Handler(ICountryRepository countryRepository) : IRequestHandler<Request, IEnumerable<CountryListItem>>
    {
        public async Task<IEnumerable<CountryListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            return await countryRepository.GetAll()
                .Select(c => c.MapToDto())
                .ToListAsync(cancellationToken);
        }
    }
}