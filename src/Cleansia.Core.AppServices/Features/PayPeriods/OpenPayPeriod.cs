using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PayPeriods;

public class OpenPayPeriod
{
    public record Command(
        string PayPeriodId,
        string? Notes) : ICommand<Response>;

    public record Response(string PayPeriodId);

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IPayPeriodRepository _payPeriodRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IPayPeriodRepository payPeriodRepository) : base(userRepository, userSessionProvider)
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

            RuleFor(x => x.Notes)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> BeClosedStatusAsync(string payPeriodId, CancellationToken cancellationToken)
        {
            var period = await _payPeriodRepository.GetByIdAsync(payPeriodId, cancellationToken);
            return period!.Status == PayPeriodStatus.Closed;
        }
    }

    public class Handler(
        IPayPeriodRepository payPeriodRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payPeriod = await payPeriodRepository.GetByIdAsync(command.PayPeriodId, cancellationToken);

            payPeriod!.Reopen(command.Notes);

            return BusinessResult.Success(new Response(payPeriod.Id));
        }
    }
}
