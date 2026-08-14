using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The admin's read of an order's entry instructions — <i>"key under the mat"</i>, <i>"gate code 4455"</i>.
///
/// <para><b>It is a Command, and that is what makes it auditable.</b> <c>AdminMutationGate</c> records an
/// admin action only when the request type name ends in <c>Command</c>; a query produces no audit row and
/// has no commit for one to ride. Mirrors <c>RevealEmployeePayoutDetails</c> for exactly that reason —
/// the audit trail is the compensating control for holding a value this sensitive at all, so an
/// unaudited reveal would be worse than no reveal route.</para>
///
/// <para>Unlike the payout reveal it stamps nothing on the entity: a <c>LastRevealedAt</c> column would
/// cost a migration to record what the audit row already records. The requirement was "who read this,
/// and when", and the audit row answers it.</para>
///
/// <para>The assigned cleaner's access is unchanged and still comes through the ordinary order detail —
/// they are standing at the door. A <i>browsing</i> cleaner has never seen this field.</para>
/// </summary>
[AuditAction("order.access_instructions.reveal", ResourceType = "Order")]
public class RevealOrderAccessInstructions
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public record Command(string OrderId) : ICommand<RevealedAccessInstructions>;

    /// <summary>The revealed text, plus whether there was any — an empty field is a real answer.</summary>
    public record RevealedAccessInstructions(string OrderId, string? AccessInstructions);

    /// Ids only (ADR-0012 D4.1) — the revealed value is exactly what the audit log must never copy.
    public record RevealSnapshot(string OrderId, bool HadInstructions);

    public class Handler(
        IOrderRepository orderRepository,
        IAuditContext auditContext) : ICommandHandler<Command, RevealedAccessInstructions>
    {
        public async Task<BusinessResult<RevealedAccessInstructions>> Handle(
            Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);
            if (order is null)
            {
                return BusinessResult.Failure<RevealedAccessInstructions>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var snapshot = new RevealSnapshot(
                order.Id, !string.IsNullOrWhiteSpace(order.AccessInstructions));
            auditContext.RecordChange("Order", order.Id, snapshot, snapshot);

            return BusinessResult.Success(
                new RevealedAccessInstructions(order.Id, order.AccessInstructions));
        }
    }
}
