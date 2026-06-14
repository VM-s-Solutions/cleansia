using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class AddDisputeMessage
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(IDisputeRepository disputeRepository)
        {
            RuleFor(x => x.DisputeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(disputeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.DisputeNotFound);

            RuleFor(x => x.Message)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);
        }
    }

    public record Command(
        string DisputeId,
        string Message,
        bool IsStaffMessage
    ) : ICommand;

    public class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider,
        IPendingDispatch pending) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var dispute = await disputeRepository.GetForUpdateAsync(request.DisputeId, cancellationToken);

            // ADR-0001 §D2 Note C: the staff flag is DERIVED from the caller's profile,
            // never trusted from the request body. A customer-host caller can flip
            // Command.IsStaffMessage, but only a genuine Administrator can author a staff reply
            // (staff dispute replies are Admin-only — Q-0005). The host also constructs the command
            // per-audience (outer seam); this is the inner gate that holds on every invocation path.
            var isAdmin = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value
                == UserProfile.Administrator.ToString();
            var isStaffMessage = request.IsStaffMessage && isAdmin;

            if (!isStaffMessage && dispute.UserId != userId)
            {
                return BusinessResult.Failure(new Error(
                    nameof(request.DisputeId), BusinessErrorMessage.DisputeNotOwnedByUser));
            }

            dispute.AddMessage(
                message: request.Message,
                authorId: userId,
                isStaff: isStaffMessage
            );

            // Push only for support → customer direction. Customer-authored
            // messages don't notify the customer back; admin-side staff get
            // their own dashboard alerts (out of scope here).
            if (isStaffMessage && !string.IsNullOrEmpty(dispute.UserId))
            {
                // Subject for the push dedup key is the dispute (no order on this path).
                pending.Enqueue(
                    QueueNames.NotificationsDispatch,
                    new QueueEnvelope<SendPushNotificationMessage>(
                        MessageKeys.Push(dispute.UserId, NotificationEventCatalog.DisputeReply, dispute.Id),
                        dispute.TenantId,
                        new SendPushNotificationMessage(
                            UserId: dispute.UserId,
                            EventKey: NotificationEventCatalog.DisputeReply,
                            Args: new Dictionary<string, string>
                            {
                                ["disputeId"] = dispute.Id,
                            },
                            TenantId: dispute.TenantId)),
                    MessageKeys.Push(dispute.UserId, NotificationEventCatalog.DisputeReply, dispute.Id));
            }

            return BusinessResult.Success();
        }
    }
}
