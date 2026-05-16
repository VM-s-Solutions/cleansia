using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
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
        IQueueClient queueClient) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var dispute = await disputeRepository.GetDisputeWithDetailsAsync(request.DisputeId);

            if (!request.IsStaffMessage && dispute.UserId != userId)
            {
                return BusinessResult.Failure(new Error(
                    nameof(request.DisputeId), BusinessErrorMessage.DisputeNotOwnedByUser));
            }

            dispute.AddMessage(
                message: request.Message,
                authorId: userId,
                isStaff: request.IsStaffMessage
            );

            // Push only for support → customer direction. Customer-authored
            // messages don't notify the customer back; admin-side staff get
            // their own dashboard alerts (out of scope here).
            if (request.IsStaffMessage && !string.IsNullOrEmpty(dispute.UserId))
            {
                await queueClient.SendAsync(
                    QueueNames.NotificationsDispatch,
                    new SendPushNotificationMessage(
                        UserId: dispute.UserId,
                        EventKey: NotificationEventCatalog.DisputeReply,
                        Args: new Dictionary<string, string>
                        {
                            ["disputeId"] = dispute.Id,
                        },
                        TenantId: dispute.TenantId),
                    cancellationToken);
            }

            return BusinessResult.Success();
        }
    }
}
