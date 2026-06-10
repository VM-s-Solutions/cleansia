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

    internal class Handler(IPackageRepository packageRepository)
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
                .Select(package => package.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
