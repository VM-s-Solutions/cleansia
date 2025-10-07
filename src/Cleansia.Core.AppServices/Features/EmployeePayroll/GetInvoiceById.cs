#nullable enable
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class GetInvoiceById
{
    public record Query(string InvoiceId) : IRequest<EmployeeInvoiceDetailDto?>;

    internal class Handler(
        IEmployeeInvoiceRepository invoiceRepository)
        : IRequestHandler<Query, EmployeeInvoiceDetailDto?>
    {
        public async Task<EmployeeInvoiceDetailDto?> Handle(Query request, CancellationToken cancellationToken)
        {
            var invoice = await invoiceRepository
                .GetAll()
                .Include(i => i.Employee)
                    .ThenInclude(e => e.User)
                .Include(i => i.PayPeriod)
                .Include(i => i.Currency)
                .Include(i => i.OrderPays)
                    .ThenInclude(op => op.Order)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

            return invoice?.MapToDetailDto();
        }
    }
}
