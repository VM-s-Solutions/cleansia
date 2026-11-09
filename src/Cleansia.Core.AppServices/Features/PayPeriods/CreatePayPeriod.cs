using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.PayPeriods;

public class CreatePayPeriod
{
    public record Command(
        DateOnly StartDate,
        string? Notes) : ICommand<Response>;

    public record Response(string PayPeriodId);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IPayPeriodRepository _payPeriodRepository;
        private readonly IPayPeriodClosingConfig _payPeriodClosingConfig;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IPayPeriodRepository payPeriodRepository,
            IPayPeriodClosingConfig payPeriodClosingConfig)
        {
            _payPeriodRepository = payPeriodRepository;
            _payPeriodClosingConfig = payPeriodClosingConfig;

            RuleFor(x => x).SetValidator(new UserEmailValidator<Command>(userRepository, userSessionProvider));

            RuleFor(x => x.StartDate)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x)
                .MustAsync(BeNoOverlappingPeriodAsync)
                .WithMessage(BusinessErrorMessage.OverlappingPeriod);

            RuleFor(x => x.Notes)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> BeNoOverlappingPeriodAsync(Command command, CancellationToken cancellationToken)
        {
            var hasOverlap = await _payPeriodRepository.HasOverlappingPeriodAsync(
                command.StartDate, command.StartDate.AddDays(_payPeriodClosingConfig.LengthInDays - 1), null, cancellationToken);
            return !hasOverlap;
        }
    }

    public class Handler(
        IPayPeriodRepository payPeriodRepository,
        IPayPeriodClosingConfig payPeriodClosingConfig)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payPeriod = PayPeriod.CreateBiWeekly(command.StartDate, payPeriodClosingConfig.LengthInDays, command.Notes);

            payPeriodRepository.Add(payPeriod);

            return BusinessResult.Success(new Response(payPeriod.Id));
        }
    }
}
