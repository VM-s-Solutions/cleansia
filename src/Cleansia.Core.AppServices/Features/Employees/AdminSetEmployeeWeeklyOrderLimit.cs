#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

/// <summary>
/// Caps — or un-caps — how many orders one cleaner may hold in a Monday-to-Sunday week.
///
/// <para><b>A narrow command rather than a field on <see cref="AdminUpdateEmployee"/>.</b> That command
/// treats every field as nullable and merges with <c>?? existing</c>, so <see langword="null"/> there
/// means "not supplied". It structurally cannot express <i>clear this cleaner's cap back to
/// unlimited</i>, which is half of what this feature is for. <see cref="AdminUpdateEmployeeAvailability"/>
/// is the in-repo precedent for a narrow admin sub-command with its own endpoint and audit label.</para>
///
/// <para>Unlimited is the default for every cleaner and is expected to stay that way; a cap is a
/// deliberate act against one person, not a rule the platform applies to everyone.</para>
/// </summary>
[AuditAction("employee.weekly_limit.update", ResourceType = "User")]
public class AdminSetEmployeeWeeklyOrderLimit
{
    /// <param name="WeeklyOrderLimit">Null clears the cap back to unlimited.</param>
    public record Command(string EmployeeId, int? WeeklyOrderLimit) : ICommand<Response>;

    public record Request(int? WeeklyOrderLimit);

    public record Response(string EmployeeId, int? WeeklyOrderLimit);

    /// <summary>
    /// Unlike its sibling snapshots this one carries the VALUES, not just ids: a weekly order limit is
    /// an integer the platform chose, not personal data belonging to the subject, so the audit trail
    /// can answer "what was it before" instead of only "someone changed something".
    /// </summary>
    public record WeeklyLimitSnapshot(string UserId, string EmployeeId, int? WeeklyOrderLimit);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IEmployeeRepository employeeRepository)
        {
            RuleFor(c => c.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound);

            // One is the floor, not zero. A zero cap is a cleaner who may take nothing, which is what
            // ContractStatus is for — expressing a suspension as a quiet number would hide it from
            // every screen that reads the contract instead.
            RuleFor(c => c.WeeklyOrderLimit)
                .GreaterThanOrEqualTo(1)
                .WithMessage(BusinessErrorMessage.WeeklyOrderLimitInvalid)
                .When(c => c.WeeklyOrderLimit.HasValue);
        }
    }

    public class Handler(
        IEmployeeRepository employeeRepository,
        INotificationProducer notificationProducer,
        IAuditContext auditContext) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            // The validator proved existence, but a load that diverges from its check is how a 500
            // replaces a business error — guard rather than null-forgive.
            if (employee == null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.EmployeeId), BusinessErrorMessage.NotFound));
            }

            var before = new WeeklyLimitSnapshot(employee.UserId, employee.Id, employee.WeeklyOrderLimit);

            employee.SetWeeklyOrderLimit(command.WeeklyOrderLimit);

            var after = new WeeklyLimitSnapshot(employee.UserId, employee.Id, employee.WeeklyOrderLimit);
            auditContext.RecordChange("User", employee.UserId, before, after);

            await NotifyIfTightenedAsync(
                employee.UserId, employee.TenantId, before.WeeklyOrderLimit, after.WeeklyOrderLimit,
                DateTime.UtcNow, cancellationToken);

            return BusinessResult.Success(new Response(employee.Id, employee.WeeklyOrderLimit));
        }

        /// <summary>
        /// Tell the cleaner only when the cap TIGHTENS — it appeared where there was none, or it moved
        /// down. Q-CAP-01, ruled by the owner 2026-08-24.
        ///
        /// <para>A rise and a clear are deliberately silent. Both only ever give, and a notice on every
        /// movement would train the cleaner to swipe past the one that takes. Silence on a no-op change
        /// is the same argument: an admin re-saving the same number is not news.</para>
        ///
        /// <para><b>The subject is the MOMENT of the change, and it has to be.</b> A cap change mints no
        /// row, so there is no entity id to name the event with — and every cheaper candidate collides.
        /// Keyed on the employee, a second tightening reuses the first key; keyed on the new value,
        /// 5 → 10 → 5 reuses it too. A collision here is not a dropped push: the outbox's
        /// <c>(QueueName, MessageKey)</c> unique index raises inside the pipeline's <c>CommitAsync</c>,
        /// after this handler returned, so it would roll the admin's change back. That is the defect
        /// this repository has now paid for five times. The timestamp is captured ONCE by the caller,
        /// so a retry inside one request still dedups to a single row.</para>
        /// </summary>
        private async Task NotifyIfTightenedAsync(
            string userId,
            string? tenantId,
            int? before,
            int? after,
            DateTime changedAtUtc,
            CancellationToken cancellationToken)
        {
            if (after is not { } limit)
            {
                return;
            }

            var tightened = before is not { } previous || limit < previous;
            if (!tightened)
            {
                return;
            }

            await notificationProducer.NotifyAsync(
                userId,
                NotificationEventCatalog.EmployeeWeeklyLimitSet,
                new Dictionary<string, string>
                {
                    ["count"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                tenantId,
                $"{limit}:{changedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture)}",
                cancellationToken);
        }
    }
}
