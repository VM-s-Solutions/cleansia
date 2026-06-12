using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PayPeriods;

public class MarkPayPeriodPaid
{
    public record Command(string PayPeriodId) : ICommand<Response>;

    public record Response(string PayPeriodId);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IPayPeriodRepository _payPeriodRepository;

        public Validator(IPayPeriodRepository payPeriodRepository)
        {
            _payPeriodRepository = payPeriodRepository;

            RuleFor(x => x.PayPeriodId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payPeriodRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotFound)
                .MustAsync(BeClosedStatusAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotClosed);
        }

        private async Task<bool> BeClosedStatusAsync(string payPeriodId, CancellationToken cancellationToken)
        {
            var period = await _payPeriodRepository.GetByIdAsync(payPeriodId, cancellationToken);
            return period!.Status == PayPeriodStatus.Closed;
        }
    }

    public class Handler(IPayPeriodRepository payPeriodRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payPeriod = await payPeriodRepository.GetByIdAsync(command.PayPeriodId, cancellationToken);

            if (payPeriod!.Status != PayPeriodStatus.Closed)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PayPeriodId), BusinessErrorMessage.PayPeriodNotClosed));
            }

            payPeriod.MarkAsPaid();

            return BusinessResult.Success(new Response(payPeriod.Id));
        }
    }
}
