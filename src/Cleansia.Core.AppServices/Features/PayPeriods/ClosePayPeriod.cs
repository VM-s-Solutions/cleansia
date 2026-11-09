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

public class ClosePayPeriod
{
    public record Command(
        string PayPeriodId,
        string Reason) : ICommand<Response>;

    public record Response(string PayPeriodId);

    public record PeriodSnapshot(string PayPeriodId, DateOnly EndDate, PayPeriodStatus Status);

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IPayPeriodRepository _payPeriodRepository;
        private readonly TimeProvider _timeProvider;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IPayPeriodRepository payPeriodRepository,
            TimeProvider timeProvider) : base(userRepository, userSessionProvider)
        {
            _payPeriodRepository = payPeriodRepository;
            _timeProvider = timeProvider;

            RuleFor(x => x.PayPeriodId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payPeriodRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotFound)
                .MustAsync(BeOpenStatusAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotOpen)
                .MustAsync(HaveStartedAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotStarted);

            RuleFor(x => x.Reason)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> BeOpenStatusAsync(string payPeriodId, CancellationToken cancellationToken)
        {
            var period = await _payPeriodRepository.GetByIdAsync(payPeriodId, cancellationToken);
            return period!.Status == PayPeriodStatus.Open;
        }

        private async Task<bool> HaveStartedAsync(string payPeriodId, CancellationToken cancellationToken)
        {
            var period = await _payPeriodRepository.GetByIdAsync(payPeriodId, cancellationToken);
            return period!.StartDate <= DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        }
    }

    public class Handler(
        IPayPeriodRepository payPeriodRepository,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext,
        IPayPeriodClosingConfig payPeriodClosingConfig,
        TimeProvider timeProvider,
        ITenantRepository tenantRepository,
        ITenantProvider tenantProvider)
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

            var before = new PeriodSnapshot(payPeriod.Id, payPeriod.EndDate, payPeriod.Status);
            payPeriod.Close(userSessionProvider.GetUserEmail()!, endDay: DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

            // The close is not saved yet, so the store still reads this period as Open.
            var anotherIsOpen = await payPeriodRepository
                .GetQueryable()
                .AnyAsync(p => p.Status == PayPeriodStatus.Open && p.Id != payPeriod.Id, cancellationToken);
            // A deactivated company has no next period, as the night run holds (ADR-0064 D2).
            var tenantId = tenantProvider.GetCurrentTenantId();
            var company = tenantId is null ? null : await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (!anotherIsOpen && company is not { IsDeactivated: true })
            {
                payPeriodRepository.Add(PayPeriod.CreateBiWeekly(payPeriod.EndDate.AddDays(1), payPeriodClosingConfig.LengthInDays));
            }

            auditContext.RecordChange(
                "PayPeriod",
                payPeriod.Id,
                before,
                new PeriodSnapshot(payPeriod.Id, payPeriod.EndDate, payPeriod.Status),
                command.Reason.Trim());

            // Flushed here so a lost race is a business error, not a 500 from the pipeline's commit: another
            // close of this period committed first, and the one-open index refuses this successor.
            try
            {
                await payPeriodRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PayPeriodId), BusinessErrorMessage.PayPeriodNotOpen));
            }

            return BusinessResult.Success(new Response(payPeriod.Id));
        }
    }
}
