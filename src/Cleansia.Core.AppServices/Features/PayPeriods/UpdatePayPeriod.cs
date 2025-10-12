using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayPeriods;

public class UpdatePayPeriod
{
    public record Command(
        string PayPeriodId,
        DateOnly StartDate,
        DateOnly EndDate,
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
                .MustAsync(BeOpenStatusAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotOpen);

            RuleFor(x => x.StartDate)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.EndDate)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must((cmd, endDate) => endDate > cmd.StartDate)
                .WithMessage(BusinessErrorMessage.InvalidDate)
                .Must((cmd, endDate) =>
                {
                    var duration = endDate.DayNumber - cmd.StartDate.DayNumber;
                    return duration is >= 7 and <= 31;
                })
                .WithMessage(BusinessErrorMessage.InvalidDuration);

            RuleFor(x => x)
                .MustAsync(BeNoOverlappingPeriodAsync)
                .WithMessage(BusinessErrorMessage.OverlappingPeriod);

            RuleFor(x => x.Notes)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private Task<bool> BeOpenStatusAsync(string payPeriodId, CancellationToken cancellationToken) =>
            _payPeriodRepository.GetByIdAsync(payPeriodId, cancellationToken)
                .ContinueWith(t => t.Result!.Status == PayPeriodStatus.Open, cancellationToken);

        private Task<bool> BeNoOverlappingPeriodAsync(Command command, CancellationToken cancellationToken) =>
            _payPeriodRepository.HasOverlappingPeriodAsync(command.StartDate, command.EndDate, command.PayPeriodId, cancellationToken)
                .ContinueWith(t => !t.Result, cancellationToken);
    }

    public class Handler(
        IPayPeriodRepository payPeriodRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payPeriod = await payPeriodRepository.GetByIdAsync(command.PayPeriodId, cancellationToken);

            payPeriod!.Update(command.StartDate, command.EndDate, command.Notes);

            return BusinessResult.Success(new Response(payPeriod.Id));
        }
    }
}
