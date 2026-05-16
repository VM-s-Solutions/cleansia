using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
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
        IEmployeeInvoiceRepository invoiceRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider)
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

            if (invoice == null)
            {
                return BusinessResult.Failure<EmployeeInvoiceDetailDto>(new Error(
                    nameof(request.InvoiceId), BusinessErrorMessage.InvoiceNotFound));
            }

            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            if (role != UserProfile.Administrator.ToString())
            {
                var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
                if (string.IsNullOrEmpty(employeeId) || invoice.EmployeeId != employeeId)
                {
                    return BusinessResult.Failure<EmployeeInvoiceDetailDto>(new Error(
                        nameof(request.InvoiceId), BusinessErrorMessage.InvoiceNotFound));
                }
            }

            return BusinessResult.Success(invoice.MapToDetailDto());
        }
    }
}
