using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Disputes;

// Admin audience: only the admin arm records it, so a customer's or a cleaner's message stays unrecorded
// (ADR-0062 D3 — the dispute row is its own durable record of author and time).
[AuditAction("dispute.message.add", ResourceType = "Dispute")]
public class AddDisputeMessage
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(IDisputeRepository disputeRepository, IUserSessionProvider userSessionProvider)
        {
            RuleFor(x => x.DisputeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync((id, ct) => DisputeReads.ExistsForCallerAsync(disputeRepository, userSessionProvider, id, ct))
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
        INotificationProducer notificationProducer) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;

            // ADR-0001 §D2 Note C: the staff flag is DERIVED from the caller's profile,
            // never trusted from the request body. A customer-host caller can flip
            // Command.IsStaffMessage, but only a genuine Administrator can author a staff reply
            // (staff dispute replies are Admin-only — Q-0005). The host also constructs the command
            // per-audience (outer seam); this is the inner gate that holds on every invocation path.
            var isAdmin = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value
                == UserProfile.Administrator.ToString();
            var isStaffMessage = request.IsStaffMessage && isAdmin;

            // An admin reaches their company's disputes through the filter; a customer reaches their own
            // in every operating company — the dispute carries its order's operator, and the customer
            // may have booked across the border (S8: pinned by the caller's own id).
            var dispute = isAdmin
                ? await disputeRepository.GetForUpdateAsync(request.DisputeId, cancellationToken)
                : await disputeRepository.GetQueryableForOwner(userId)
                    .Include(d => d.Order)
                    .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken);

            if (dispute is null || (!isStaffMessage && dispute.UserId != userId))
            {
                return BusinessResult.Failure(new Error(
                    nameof(request.DisputeId), BusinessErrorMessage.DisputeNotOwnedByUser));
            }

            var added = dispute.AddMessage(
                message: request.Message,
                authorId: userId,
                isStaff: isStaffMessage
            );

            // Push only for support → customer direction. Customer-authored
            // messages don't notify the customer back; admin-side staff get
            // their own dashboard alerts (out of scope here).
            if (isStaffMessage && !string.IsNullOrEmpty(dispute.UserId))
            {
                // The dedup subject is the MESSAGE, not the dispute. A dispute is a conversation, so
                // keying it on the dispute meant the second staff reply minted a key the first had
                // already written — and the outbox's unique index raises that at the pipeline's
                // commit, which rolls the whole transaction back. The customer did not merely miss a
                // push: the reply was never saved, and support saw a 500. Every dispute with two
                // replies hit it.
                await notificationProducer.NotifyAsync(
                    dispute.UserId,
                    NotificationEventCatalog.DisputeReply,
                    new Dictionary<string, string>
                    {
                        ["disputeId"] = dispute.Id,
                    },
                    dispute.TenantId,
                    added.Id,
                    cancellationToken);
            }

            return BusinessResult.Success();
        }
    }
}
