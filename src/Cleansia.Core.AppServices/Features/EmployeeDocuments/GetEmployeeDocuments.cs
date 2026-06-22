using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
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

    internal class Handler(IEmployeeDocumentRepository documentRepository)
        : IRequestHandler<Request, PagedData<EmployeeDocumentItem>>
    {
        public async Task<PagedData<EmployeeDocumentItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = EmployeeDocumentSpecification.Create(
                isActive: request.Filter?.IsActive ?? true,
                employeeId: request.Filter?.EmployeeId,
                documentType: request.Filter?.DocumentType,
                status: request.Filter?.Status,
                latestVersionOnly: request.Filter?.LatestVersionOnly
            );

            var filter = specification.SatisfiedBy();

            var total = await documentRepository.GetCountAsync(filter, cancellationToken);
            var documents = await documentRepository
                .GetPagedSort<EmployeeDocumentSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Select(d => d.MapToDto())
                .ToListAsync(cancellationToken);

            return documents.MapToDto(total, request);
        }
    }
}
