using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayPeriods.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PayPeriods;

public class GetPayPeriodById
{
    public record Query(string PayPeriodId) : IQuery<PayPeriodDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IPayPeriodRepository payPeriodRepository)
        {
            RuleFor(x => x.PayPeriodId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payPeriodRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotFound);
        }
    }

    internal class Handler(
        IPayPeriodRepository payPeriodRepository,
        IPayPeriodClosingConfig payPeriodClosingConfig,
        ICompanySettlementReader settlementReader,
        IEmployeeInvoiceRepository employeeInvoiceRepository)
        : IQueryHandler<Query, PayPeriodDto>
    {
        public async Task<BusinessResult<PayPeriodDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var payPeriod = await payPeriodRepository.GetByIdAsync(query.PayPeriodId, cancellationToken);
            if (payPeriod is null)
            {
                return BusinessResult.Failure<PayPeriodDto>(new Error(
                    nameof(query.PayPeriodId), BusinessErrorMessage.PayPeriodNotFound));
            }

            var (unpaidInvoices, uninvoicedPayRows) =
                await settlementReader.ReadPayPeriodAsync(payPeriod.Id, cancellationToken);
            var invoiceCount = await employeeInvoiceRepository.GetCountAsync(
                i => i.PayPeriodId == payPeriod.Id, cancellationToken);

            return BusinessResult.Success(payPeriod.MapToDto(payPeriodClosingConfig.LengthInDays) with
            {
                InvoiceCount = invoiceCount,
                UnpaidInvoiceCount = unpaidInvoices,
                UninvoicedPayCount = uninvoicedPayRows,
            });
        }
    }
}
