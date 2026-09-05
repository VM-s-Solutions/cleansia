using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class CreateDispute
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.Reason)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.Description)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(DisputeLimits.DescriptionMin)
                .WithMessage(BusinessErrorMessage.MinLength)
                .MaximumLength(DisputeLimits.DescriptionMax)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);
        }
    }

    public record Command(
        string OrderId,
        DisputeReason Reason,
        string Description
    ) : ICommand<Response>;

    public record Response(string DisputeId);

    public class Handler(
        IDisputeRepository disputeRepository,
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;

            // Inner ownership gate (ADR-0001 §D2 [OWN-DATA], S3): the
            // CanCreateDispute → CustomerOnly policy is the coarse outer gate; this
            // handler check decides *which* customer's order may be disputed and holds
            // on any invocation path. Loaded via the tenant-filtered GetByIdAsync (S8 —
            // never IgnoreQueryFilters). A non-owner gets the not-found business error
            // (NotFound, not Forbidden) so a missing order and someone else's order are
            // indistinguishable.
            var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

            if (order is null || order.UserId != userId)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(request.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // You cannot report a clean that has not happened yet. Nothing stopped it before: the
            // validator checks the order exists, the reason enum and the description length, and
            // there was no timing rule of any kind — so a dispute could be filed against tomorrow's
            // booking, or one from last year.
            //
            // The gate is the SCHEDULED TIME, not the order status, and that distinction matters. A
            // status gate ("must be Completed") would refuse the one case the guarantee exists for:
            // a cleaner who never arrives leaves the order sitting at Confirmed or OnTheWay forever,
            // with no completion to point at. The slot passing is what makes a no-show reportable.
            if (order.CleaningDateTime > DateTime.UtcNow)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(request.OrderId), BusinessErrorMessage.DisputeCleaningNotStarted));
            }

            var existingDispute = await disputeRepository.GetOpenDisputeForOrderAsync(
                request.OrderId, cancellationToken);

            if (existingDispute != null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(request.OrderId), BusinessErrorMessage.DisputeAlreadyExists));
            }

            var dispute = new Dispute(
                orderId: request.OrderId,
                userId: userId,
                reason: request.Reason,
                description: request.Description,
                createdBy: userId
            );

            disputeRepository.Add(dispute);

            return BusinessResult.Success(new Response(dispute.Id));
        }
    }
}
