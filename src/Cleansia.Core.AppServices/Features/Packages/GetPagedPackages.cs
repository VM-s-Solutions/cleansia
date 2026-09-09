using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Features.Packages.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Packages;

public class GetPagedPackages
{
    public class Request : DataRangeRequest, IRequest<PagedData<PackageListItem>>
    {
        public PackageFilter? Filter { get; init; }
    }

    internal class Handler(
        IPackageRepository packageRepository,
        IPackagePriceRepository packagePriceRepository,
        ICurrencyRepository currencyRepository)
        : IRequestHandler<Request, PagedData<PackageListItem>>
    {
        public async Task<PagedData<PackageListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = PackageSpecification.Create(
                searchTerm: request.Filter?.SearchTerm,
                isActive: request.Filter?.IsActive
            );

            var filter = specification.SatisfiedBy();

            var totalItems = await packageRepository.GetCountAsync(filter, cancellationToken);
            var items = await packageRepository
                .GetPagedSort<PackageSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
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
            var prices = await CataloguePriceLookup.ForPackagesAsync(
                packagePriceRepository, items.Select(p => p.Id).ToList(), currency.Id, cancellationToken);
            var dtos = items
                .Select(package => package.MapToDto(prices.GetValueOrDefault(package.Id, 0m)))
                .ToList();

            return dtos.MapToDto(totalItems, request);
        }
    }
}
