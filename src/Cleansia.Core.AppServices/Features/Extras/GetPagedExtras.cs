using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Features.Extras.DTOs;
using Cleansia.Core.AppServices.Features.Extras.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Extras;

public class GetPagedExtras
{
    public class Request : DataRangeRequest, IRequest<PagedData<ExtraListItem>>
    {
        public ExtraFilter? Filter { get; init; }
    }

    internal class Handler(
        IExtraRepository extraRepository,
        IExtraPriceRepository extraPriceRepository,
        ICurrencyRepository currencyRepository)
        : IRequestHandler<Request, PagedData<ExtraListItem>>
    {
        public async Task<PagedData<ExtraListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var filter = ExtraSpecification.Create(
                searchTerm: request.Filter?.SearchTerm,
                isActive: request.Filter?.IsActive).SatisfiedBy();

            var totalItems = await extraRepository.GetCountAsync(filter, cancellationToken);
            var items = await extraRepository
                .GetPagedSort<ExtraSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // The admin list does NOT filter on having a price -- an admin has to be able to SEE an
            // unpriced entry in order to price it. Platform default currency's row, 0 where there is
            // none. Same reasoning as GetPagedServices; the customer read (GetExtraOverview) withholds.
            var currency = await currencyRepository.GetDefaultAsync(cancellationToken);
            var prices = await CataloguePriceLookup.ForExtrasAsync(
                extraPriceRepository, items.Select(e => e.Id).ToList(), currency.Id, cancellationToken);

            return items
                .Select(extra => extra.MapToDto(prices.GetValueOrDefault(extra.Id, 0m)))
                .ToList()
                .MapToDto(totalItems, request);
        }
    }
}
