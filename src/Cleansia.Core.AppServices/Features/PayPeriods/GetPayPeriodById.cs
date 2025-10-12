using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayPeriods.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
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
        IPayPeriodRepository payPeriodRepository)
        : IQueryHandler<Query, PayPeriodDto>
    {
        public async Task<BusinessResult<PayPeriodDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var payPeriod = await payPeriodRepository.GetByIdAsync(query.PayPeriodId, cancellationToken);

            return BusinessResult.Success(payPeriod!.MapToDto());
        }
    }
}
