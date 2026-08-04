using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

/// <summary>
/// The admin's unmasked read of a cleaner's payout destination (ADR-0034 D8.4).
///
/// <para><b>It is a Command, and that is what makes it auditable.</b> <c>AdminMutationGate</c> records an
/// admin action only when the request type name ends in <c>Command</c>; a query produces no audit row and
/// has no commit for one to ride. Since the audit trail is the compensating control for holding these
/// values in plaintext, an unaudited reveal would be worse than no reveal route at all. The reveal is
/// also a genuine state change — it stamps <c>LastRevealedAt</c> and increments <c>RevealCount</c>.</para>
/// </summary>
[AuditAction("employee.payout_details.reveal", ResourceType = "User")]
public class RevealEmployeePayoutDetails
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.EmployeeId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public record Command(string EmployeeId) : ICommand<RevealedPayoutDetails>;

    /// Ids only (ADR-0012 D4.1) — the revealed value is exactly what the audit log must never copy.
    public record RevealSnapshot(string UserId, string EmployeeId, int RevealCount);

    public class Handler(
        IEmployeeRepository employeeRepository,
        IEmployeePayoutDetailsRepository payoutDetailsRepository,
        IAuditContext auditContext) : ICommandHandler<Command, RevealedPayoutDetails>
    {
        public async Task<BusinessResult<RevealedPayoutDetails>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);
            var details = employee is null
                ? null
                : await payoutDetailsRepository.GetByEmployeeIdAsync(employee.Id, cancellationToken);

            if (employee is null || details is null)
            {
                return BusinessResult.Failure<RevealedPayoutDetails>(
                    new Error(nameof(command.EmployeeId), BusinessErrorMessage.PayoutDetailsNotFound));
            }

            details.MarkRevealed(DateTime.UtcNow);

            var snapshot = new RevealSnapshot(employee.UserId, employee.Id, details.RevealCount);
            auditContext.RecordChange("User", employee.UserId, snapshot, snapshot);

            return BusinessResult.Success(details.MapToRevealedDto());
        }
    }
}
