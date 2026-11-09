using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
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

    public record EndDateSnapshot(string PayPeriodId, DateOnly EndDate);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IPayPeriodRepository _payPeriodRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IPayPeriodRepository payPeriodRepository,
            IPayPeriodClosingConfig payPeriodClosingConfig,
            TimeProvider timeProvider)
        {
            _payPeriodRepository = payPeriodRepository;

            RuleFor(x => x).SetValidator(new UserEmailValidator<Command>(userRepository, userSessionProvider));

            RuleFor(x => x.PayPeriodId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payPeriodRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotFound)
                .MustAsync(BeOpenStatusAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotOpen)
                .MustAsync(KeepTheStoredStartAsync)
                .WithMessage(BusinessErrorMessage.InvalidDate);

            RuleFor(x => x.StartDate)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.EndDate)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must((cmd, endDate) => endDate > cmd.StartDate)
                .WithMessage(BusinessErrorMessage.InvalidDate)
                .Must(endDate => endDate > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
                .WithMessage(BusinessErrorMessage.PayPeriodEndTooSoon)
                .Must((cmd, endDate) => endDate <= cmd.StartDate.AddDays(payPeriodClosingConfig.LengthInDays - 1))
                .WithMessage(BusinessErrorMessage.InvalidDuration);

            RuleFor(x => x)
                .MustAsync(BeNoOverlappingPeriodAsync)
                .WithMessage(BusinessErrorMessage.OverlappingPeriod);

            RuleFor(x => x.Notes)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> BeOpenStatusAsync(string payPeriodId, CancellationToken cancellationToken)
        {
            var period = await _payPeriodRepository.GetByIdAsync(payPeriodId, cancellationToken);
            return period!.Status == PayPeriodStatus.Open;
        }

        private async Task<bool> KeepTheStoredStartAsync(Command command, string payPeriodId, CancellationToken cancellationToken)
        {
            var period = await _payPeriodRepository.GetByIdAsync(payPeriodId, cancellationToken);
            return period!.StartDate == command.StartDate;
        }

        private async Task<bool> BeNoOverlappingPeriodAsync(Command command, CancellationToken cancellationToken)
        {
            var hasOverlap = await _payPeriodRepository.HasOverlappingPeriodAsync(
                command.StartDate, command.EndDate, command.PayPeriodId, cancellationToken);
            return !hasOverlap;
        }
    }

    public class Handler(
        IPayPeriodRepository payPeriodRepository,
        IPayPeriodClosingConfig payPeriodClosingConfig,
        IAuditContext auditContext)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payPeriod = await payPeriodRepository.GetByIdAsync(command.PayPeriodId, cancellationToken);
            if (payPeriod is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PayPeriodId), BusinessErrorMessage.PayPeriodNotFound));
            }

            var before = new EndDateSnapshot(payPeriod.Id, payPeriod.EndDate);
            payPeriod.Update(command.StartDate, command.EndDate, payPeriodClosingConfig.LengthInDays, command.Notes);
            auditContext.RecordChange("PayPeriod", payPeriod.Id, before, new EndDateSnapshot(payPeriod.Id, payPeriod.EndDate));

            return BusinessResult.Success(new Response(payPeriod.Id));
        }
    }
}
