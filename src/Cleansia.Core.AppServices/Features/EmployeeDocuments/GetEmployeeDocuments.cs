using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeeDocuments;

public class GetEmployeeDocuments
{
    public class Request : DataRangeRequest, IRequest<PagedData<EmployeeDocumentItem>>
    {
        public EmployeeDocumentFilter? Filter { get; init; }
    }

    public class Handler(IEmployeeDocumentRepository documentRepository)
        : IRequestHandler<Request, PagedData<EmployeeDocumentItem>>
    {
        public async Task<PagedData<EmployeeDocumentItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var filter = request.Filter;

            var specification = EmployeeDocumentSpecification.Create(
                isActive: filter?.IsActive ?? true,
                employeeId: filter?.EmployeeId,
                documentType: filter?.DocumentType,
                status: filter?.Status,
                latestVersionOnly: filter?.LatestVersionOnly
            );

            var query = request.Sort is not null && request.Sort.Any()
                ? documentRepository.GetPagedSort<EmployeeDocumentSort>(
                    request.Offset,
                    request.Limit,
                    specification.SatisfiedBy(),
                    request.Sort.MapToDomain())
                : documentRepository.GetPaged(
                    request.Offset,
                    request.Limit,
                    specification.SatisfiedBy());

            var documents = await query
                .Select(d => d.MapToDto())
                .ToListAsync(cancellationToken);

            var total = await documentRepository.GetCountAsync(
                specification.SatisfiedBy(),
                cancellationToken);

            var pageNumber = request.Limit > 0 ? (request.Offset / request.Limit) + 1 : 1;

            return new PagedData<EmployeeDocumentItem>(
                PageNumber: pageNumber,
                PageSize: request.Limit,
                Total: total,
                Data: documents
            );
        }
    }
}
