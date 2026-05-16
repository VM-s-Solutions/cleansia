#nullable enable
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Features.EmployeePayroll.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class GetPagedInvoices
{
    public class Request : DataRangeRequest, IRequest<PagedData<EmployeeInvoiceDto>>
    {
        public EmployeeInvoiceFilter? Filter { get; init; }
    }

    internal class Handler(
        IEmployeeInvoiceRepository invoiceRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider)
        : IRequestHandler<Request, PagedData<EmployeeInvoiceDto>>
    {
        public async Task<PagedData<EmployeeInvoiceDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            string? employeeIdFilter = request.Filter?.EmployeeId;

            if (role != UserProfile.Administrator.ToString())
            {
                employeeIdFilter = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
                if (string.IsNullOrEmpty(employeeIdFilter))
                {
                    return new List<EmployeeInvoiceDto>().MapToDto(0, request);
                }
            }

            var specification = EmployeeInvoiceSpecification.Create(
                employeeId: employeeIdFilter,
                payPeriodId: request.Filter?.PayPeriodId,
                statuses: request.Filter?.Statuses,
                invoiceNumber: request.Filter?.InvoiceNumber,
                minAmount: request.Filter?.MinAmount,
                maxAmount: request.Filter?.MaxAmount,
                dateFrom: request.Filter?.DateFrom,
                dateTo: request.Filter?.DateTo);

            var filter = specification.SatisfiedBy();

            var totalItems = await invoiceRepository.GetCountAsync(filter, cancellationToken);
            var items = await invoiceRepository
                .GetPagedSort<EmployeeInvoiceSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Include(i => i.Employee)
                    .ThenInclude(e => e.User)
                .Include(i => i.PayPeriod)
                .Include(i => i.Currency)
                .Select(invoice => invoice.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
