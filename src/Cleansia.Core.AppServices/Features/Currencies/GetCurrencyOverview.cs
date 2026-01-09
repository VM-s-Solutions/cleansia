using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Currencies;

public class GetCurrencyOverview
{
    public record Request : IRequest<IEnumerable<CurrencyListItem>>;

    public class Handler(ICurrencyRepository currencyRepository) : IRequestHandler<Request, IEnumerable<CurrencyListItem>>
    {
        public async Task<IEnumerable<CurrencyListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            return await currencyRepository.GetAll()
                .OrderByDescending(c => c.IsDefault)
                .ThenBy(c => c.Name)
                .Select(currency => currency.MapToDto())
                .ToListAsync(cancellationToken);
        }
    }
}