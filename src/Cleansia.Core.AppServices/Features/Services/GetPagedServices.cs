using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Features.Services.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Services;

public class GetPagedServices
{
    public class Request : DataRangeRequest, IRequest<PagedData<ServiceListItem>>
    {
        public ServiceFilter? Filter { get; init; }
    }

    internal class Handler(
        IServiceRepository serviceRepository,
        IServicePriceRepository servicePriceRepository,
        ICurrencyRepository currencyRepository)
        : IRequestHandler<Request, PagedData<ServiceListItem>>
    {
        public async Task<PagedData<ServiceListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = ServiceSpecification.Create(
                isActive: request.Filter?.IsActive,
                searchTerm: request.Filter?.SearchTerm
            );

            var filter = specification.SatisfiedBy();

            var totalItems = await serviceRepository.GetCountAsync(filter, cancellationToken);
            var items = await serviceRepository
                .GetPagedSort<ServiceSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(s => s.Category)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // The admin list does NOT filter on having a price -- an admin has to be able to SEE an
            // unpriced entry in order to price it. It shows the platform default currency's row, and 0
            // where there is none.
            //
            // That zero is reachable only for an entry whose price row was removed: create and update
            // both write one, so every entry has a default-currency price the moment it exists. When
            // per-currency authoring gets an admin surface, this list needs a column per currency
            // rather than a single number, and that is the moment to give it its own DTO.
            var currency = await currencyRepository.GetDefaultAsync(cancellationToken);
            var prices = await CataloguePriceLookup.ForServicesAsync(
                servicePriceRepository, items.Select(s => s.Id).ToList(), currency.Id, cancellationToken);
            var dtos = items
                .Select(service => service.MapToDto(
                    prices.TryGetValue(service.Id, out var p) ? p.BasePrice : 0m,
                    prices.TryGetValue(service.Id, out var q) ? q.PerRoomPrice : 0m))
                .ToList();

            return dtos.MapToDto(totalItems, request);
        }
    }
}