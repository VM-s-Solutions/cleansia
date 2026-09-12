using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.AppServices.Services.Interfaces;
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
        IUserSessionProvider userSessionProvider,
        ICurrencyResolutionService currencyResolutionService)
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

            var employee = await employeeRepository.GetByIdAsync(query.EmployeeId, cancellationToken);
            var payPeriod = await payPeriodRepository.GetByIdAsync(query.PayPeriodId, cancellationToken);
            var invoices = await employeeInvoiceRepository
                .GetAllForEmployeeAndPayPeriodAsync(query.EmployeeId, query.PayPeriodId, cancellationToken);

            // ONE currency, and everything on the DTO is in it -- the totals AND the rows, because the
            // DTO promises "the currency every amount above is denominated in". The cleaner's resolved
            // currency (the same one the partner dashboard labels with) is the view, and the invoice
            // shown is the one in it; a period may hold one invoice per currency. When the period is
            // invoiced but not in that currency, the invoice's own currency wins: the payout document
            // is what the cleaner holds, and "My Pay" disagreeing with it is the whole defect this
            // field exists to close. Pay in any other currency is not shown here; surfacing it needs a
            // currency dimension on the wire (the per-row DTO carries none yet).
            var resolved = await currencyResolutionService
                .ResolveCurrencyForEmployeeAsync(query.EmployeeId, cancellationToken);
            var invoice = invoices.FirstOrDefault(i => i.CurrencyId == resolved.Id) ?? invoices.FirstOrDefault();
            var currencyId = invoice?.CurrencyId ?? resolved.Id;
            var currencyCode = invoice is null ? resolved.Code : invoice.Currency?.Code ?? resolved.Code;

            var orderPays = (await orderEmployeePayRepository
                    .GetByEmployeeAndPeriodAsync(query.EmployeeId, query.PayPeriodId, cancellationToken))
                .Where(p => p.CurrencyId == currencyId)
                .ToList();

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
                OrderPays: orderPays.Select(p => p.MapToDto()),
                CurrencyCode: currencyCode
            );

            return BusinessResult.Success(summary);
        }
    }
}
