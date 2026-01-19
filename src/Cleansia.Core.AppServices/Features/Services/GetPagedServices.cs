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

    internal class Handler(IServiceRepository serviceRepository)
        : IRequestHandler<Request, PagedData<ServiceListItem>>
    {
        public async Task<PagedData<ServiceListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = ServiceSpecification.Create(
                searchTerm: request.Filter?.SearchTerm
            );

            var filter = specification.SatisfiedBy();

            var totalItems = await serviceRepository.GetCountAsync(filter, cancellationToken);
            var items = await serviceRepository
                .GetPagedSort<ServiceSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Select(service => service.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}