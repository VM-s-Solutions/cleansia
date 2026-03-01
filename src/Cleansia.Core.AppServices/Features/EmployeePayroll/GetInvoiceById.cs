using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class GetInvoiceById
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(IEmployeeInvoiceRepository employeeInvoiceRepository)
        {
            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeInvoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound);
        }
    }

    public record Query(string InvoiceId) : IQuery<EmployeeInvoiceDetailDto>;

    internal class Handler(
        IEmployeeInvoiceRepository invoiceRepository)
        : IQueryHandler<Query, EmployeeInvoiceDetailDto>
    {
        public async Task<BusinessResult<EmployeeInvoiceDetailDto>> Handle(Query request, CancellationToken cancellationToken)
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

            var invoiceDetail = invoice!.MapToDetailDto();
            return BusinessResult.Success(invoiceDetail);
        }
    }
}
