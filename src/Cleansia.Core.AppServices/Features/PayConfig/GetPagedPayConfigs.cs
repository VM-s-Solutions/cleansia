#nullable enable
using Cleansia.Core.AppServices.Features.PayConfig.DTOs;
using Cleansia.Core.AppServices.Features.PayConfig.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayConfig;

public class GetPagedPayConfigs
{
    public class Request : DataRangeRequest, IRequest<PagedData<EmployeePayConfigDto>>
    {
        public PayConfigFilter? Filter { get; init; }
    }

    internal class Handler(
        IEmployeePayConfigRepository payConfigRepository)
        : IRequestHandler<Request, PagedData<EmployeePayConfigDto>>
    {
        public async Task<PagedData<EmployeePayConfigDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = EmployeePayConfigSpecification.Create(
                employeeId: request.Filter?.EmployeeId,
                globalOnly: string.IsNullOrEmpty(request.Filter?.EmployeeId) ? true : null,
                serviceId: request.Filter?.ServiceId,
                packageId: request.Filter?.PackageId,
                currencyId: request.Filter?.CurrencyId);

            var filter = specification.SatisfiedBy();

            var totalItems = await payConfigRepository.GetCountAsync(filter, cancellationToken);
            var items = await payConfigRepository
                .GetPagedSort<EmployeePayConfigSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(c => c.Service)
                .Include(c => c.Package)
                .Include(c => c.Currency)
                .Include(c => c.Employee)
                    .ThenInclude(e => e!.User)
                .AsNoTracking()
                .Select(config => config.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
