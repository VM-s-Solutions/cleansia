#nullable enable
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Features.Auditing.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SortDefinition = Cleansia.Core.Domain.Sorting.Common.SortDefinition;
using SortDirection = Cleansia.Core.Domain.Sorting.Common.SortDirection;

namespace Cleansia.Core.AppServices.Features.Auditing;

public class GetPagedAdminActionAudits
{
    public class Request : DataRangeRequest, IRequest<PagedData<AdminActionAuditDto>>
    {
        public AdminActionAuditFilter? Filter { get; init; }
    }

    internal class Handler(IAdminActionAuditRepository adminActionAuditRepository)
        : IRequestHandler<Request, PagedData<AdminActionAuditDto>>
    {
        public async Task<PagedData<AdminActionAuditDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = request.Filter.MapToDomain();
            var filter = specification.SatisfiedBy();

            var totalItems = await adminActionAuditRepository.GetCountAsync(filter, cancellationToken);
            var items = await adminActionAuditRepository
                .GetPagedSort<AdminActionAuditSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .AsNoTracking()
                .Select(audit => audit.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }

        private static IEnumerable<SortDefinition> ResolveSort(Request request)
        {
            var sort = request.Sort.MapToDomain().ToList();
            return sort.Count > 0
                ? sort
                : [new SortDefinition { Field = nameof(AdminActionAudit.OccurredOn), Direction = SortDirection.Descending }];
        }
    }
}
