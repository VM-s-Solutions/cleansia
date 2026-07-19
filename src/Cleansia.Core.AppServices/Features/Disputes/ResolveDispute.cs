using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Disputes;

[AuditAction("dispute.resolve", Sensitive = true, ResourceType = "Dispute")]
public class ResolveDispute
{
    public record ResolutionSnapshot(
        string DisputeId,
        DisputeStatus Status,
        decimal? RefundAmount);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DisputeId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.ResolutionNotes)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(x => x.RefundAmount)
                .GreaterThanOrEqualTo(0)
                .When(x => x.RefundAmount.HasValue)
                .WithMessage(BusinessErrorMessage.InvalidRefundAmount);
        }
    }

    public record Command(
        string DisputeId,
        decimal? RefundAmount,
        string ResolutionNotes
    ) : ICommand;

    public class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider,
        IRefundService refundService,
        INotificationProducer notificationProducer,
        IAuditContext auditContext) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var dispute = await disputeRepository.GetForUpdateAsync(request.DisputeId, cancellationToken);

            if (dispute == null)
            {
                return BusinessResult.Failure(new Error(nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            var statusBefore = dispute.Status;
            var refundBefore = dispute.RefundAmount;

            // A terminal dispute (Resolved/Closed) is never re-resolved: a second Resolve would overwrite
            // the recorded RefundAmount/notes of the settled dispute. Resolve owns the Resolved state and
            // does not run through CanTransitionTo, so the terminal check is enforced here at the seam.
            if (dispute.IsTerminal)
            {
                return BusinessResult.Failure(new Error(nameof(request.DisputeId), BusinessErrorMessage.DisputeAlreadyResolved));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            dispute.Resolve(
                resolvedBy: actorId,
                refundAmount: request.RefundAmount,
                resolutionNotes: request.ResolutionNotes
            );

            auditContext.RecordChange(
                "Dispute",
                dispute.Id,
                new ResolutionSnapshot(dispute.Id, statusBefore, refundBefore),
                new ResolutionSnapshot(dispute.Id, dispute.Status, dispute.RefundAmount));

            if (request.RefundAmount is > 0m)
            {
                var refund = await refundService.IssueRefundAsync(
                    new RefundRequest(
                        dispute.OrderId,
                        request.RefundAmount.Value,
                        RefundReason.DisputeResolution,
                        actorId,
                        DisputeId: dispute.Id),
                    cancellationToken);

                if (refund.IsSuccess && !string.IsNullOrEmpty(dispute.UserId))
                {
                    await notificationProducer.NotifyAsync(
                        dispute.UserId,
                        NotificationEventCatalog.OrderRefunded,
                        new Dictionary<string, string>
                        {
                            ["orderId"] = dispute.OrderId,
                            // Display-only, resolved AFTER the Stripe refund settled: a missing
                            // Order must degrade to the factory's tolerated empty loc-arg, never
                            // throw and unwind the resolution while the money already moved.
                            ["orderNumber"] = dispute.Order?.DisplayOrderNumber ?? string.Empty,
                            ["disputeId"] = dispute.Id,
                        },
                        dispute.TenantId,
                        dispute.OrderId,
                        cancellationToken);
                }
            }

            return BusinessResult.Success();
        }
    }
}
