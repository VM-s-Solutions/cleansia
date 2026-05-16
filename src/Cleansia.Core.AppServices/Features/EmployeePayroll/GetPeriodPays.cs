using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class GetPeriodPays
{
    public record Query(string EmployeeId, string PayPeriodId) : IQuery<PeriodPaySummaryDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(
            IEmployeeRepository employeeRepository,
            IPayPeriodRepository payPeriodRepository)
        {
            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.PayPeriodId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payPeriodRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotFound);
        }
    }

    public class Handler(
        IEmployeeRepository employeeRepository,
        IPayPeriodRepository payPeriodRepository,
        IEmployeeInvoiceRepository employeeInvoiceRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, PeriodPaySummaryDto>
    {
        public async Task<BusinessResult<PeriodPaySummaryDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            if (role != UserProfile.Administrator.ToString())
            {
                var callerEmployeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
                if (string.IsNullOrEmpty(callerEmployeeId) || callerEmployeeId != query.EmployeeId)
                {
                    return BusinessResult.Failure<PeriodPaySummaryDto>(new Error(
                        nameof(query.EmployeeId), BusinessErrorMessage.EmployeeNotFound));
                }
            }

            var orderPays = await orderEmployeePayRepository
                .GetByEmployeeAndPeriodAsync(query.EmployeeId, query.PayPeriodId, cancellationToken);

            var employee = await employeeRepository.GetByIdAsync(query.EmployeeId, cancellationToken);
            var payPeriod = await payPeriodRepository.GetByIdAsync(query.PayPeriodId, cancellationToken);
            var invoice = await employeeInvoiceRepository.GetByEmployeeAndPayPeriodAsync(query.EmployeeId, query.PayPeriodId, cancellationToken);

            var summary = new PeriodPaySummaryDto(
                PayPeriodId: query.PayPeriodId,
                PayPeriodLabel: payPeriod?.GetPeriodLabel() ?? string.Empty,
                EmployeeId: query.EmployeeId,
                EmployeeName: employee is not null ? $"{employee.User?.FirstName} {employee.User?.LastName}".Trim() : "Unknown",
                TotalOrders: orderPays.Count,
                TotalBasePay: orderPays.Sum(p => p.BasePay),
                TotalExtrasPay: orderPays.Sum(p => p.ExtrasPay),
                TotalExpensesPay: orderPays.Sum(p => p.ExpensesPay),
                TotalBonusPay: orderPays.Sum(p => p.BonusPay),
                TotalDeductionPay: orderPays.Sum(p => p.DeductionPay),
                GrandTotal: orderPays.Sum(p => p.TotalPay),
                HasInvoice: invoice is not null,
                InvoiceId: invoice?.Id,
                OrderPays: orderPays.Select(p => p.MapToDto())
            );

            return BusinessResult.Success(summary);
        }
    }
}
