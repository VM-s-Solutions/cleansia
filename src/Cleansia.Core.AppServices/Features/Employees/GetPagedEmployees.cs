using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Features.Employees.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Employees;

public class GetPagedEmployees
{
    public class Request : DataRangeRequest, IRequest<PagedData<AdminEmployeeListItem>>
    {
        public EmployeeFilter? Filter { get; init; }
    }

    internal class Handler(IEmployeeRepository employeeRepository)
        : IRequestHandler<Request, PagedData<AdminEmployeeListItem>>
    {
        public async Task<PagedData<AdminEmployeeListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = EmployeeSpecification.Create(
                id: request.Filter?.Id,
                isActive: request.Filter?.IsActive,
                contractStatuses: request.Filter?.ContractStatuses,
                searchTerm: request.Filter?.SearchTerm
            );

            var filter = specification.SatisfiedBy();

            var totalItems = await employeeRepository.GetCountAsync(filter, cancellationToken);
            var items = await employeeRepository
                .GetPagedSort<EmployeeSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(e => e.User)
                .Include(e => e.Nationality)
                .Include(e => e.Address)
                .AsNoTracking()
                .Select(employee => employee.MapToAdminDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
