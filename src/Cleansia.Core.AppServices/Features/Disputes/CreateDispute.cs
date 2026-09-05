using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
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

            // SHAPE only. Whether a line is actually ON the order is checked in the handler, AFTER
            // the ownership gate — deliberately. FluentValidation runs first, and a membership rule
            // here would answer "does service X belong to order Y" for an order the caller does not
            // own, which is exactly the enumeration difference the handler's not-found-not-forbidden
            // shape exists to deny. The handler already loads the order graph it needs, so the check
            // costs nothing extra there and a second load here would be the only way to do it.
            RuleForEach(x => x.Lines)
                .Must(line => !string.IsNullOrWhiteSpace(line.ServiceId))
                .WithMessage(BusinessErrorMessage.Required)
                .When(x => x.Lines is not null);

            RuleFor(x => x.Lines)
                .Must(lines => lines!.Count <= DisputeLimits.MaxLines)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded)
                .When(x => x.Lines is not null);
        }
    }

    /// <summary>
    /// One item of the order the customer says was not done properly. The identity is the same
    /// <c>(ServiceId, PackageId?)</c> pair <c>IssuePartialRefund.RefundLineSelection</c> already uses,
    /// so the selection a customer authors and the one an admin authors are the same thing named the
    /// same way. A standalone service leaves <see cref="PackageId"/> null; a service inside a bundle
    /// names both.
    /// </summary>
    public record DisputeLineSelection(string ServiceId, string? PackageId);

    public record Command(
        string OrderId,
        DisputeReason Reason,
        string Description,
        /// <summary>
        /// Which items were unsatisfactory. Optional and empty by default — a dispute about the whole
        /// job, or about a charge, names no lines at all.
        /// </summary>
        IReadOnlyList<DisputeLineSelection>? Lines = null
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

            // Behind the ownership gate, against the graph GetByIdAsync already loaded.
            var selected = request.Lines ?? [];
            if (selected.Count > 0)
            {
                var onOrder = OrderLineIdentities(order);
                if (selected.Any(line => !onOrder.Contains((line.ServiceId, line.PackageId))))
                {
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(request.Lines), BusinessErrorMessage.DisputeLineNotOnOrder));
                }
            }

            var dispute = new Dispute(
                orderId: request.OrderId,
                userId: userId,
                reason: request.Reason,
                description: request.Description,
                createdBy: userId
            );

            if (selected.Count > 0)
            {
                dispute.AddLines(selected.Select(l => (l.ServiceId, l.PackageId)), userId);
            }

            disputeRepository.Add(dispute);

            return BusinessResult.Success(new Response(dispute.Id));
        }

        /// <summary>
        /// Every item identity this order actually contains — standalone services, and the services
        /// inside each package. Mirrors <c>IssuePartialRefund.BuildOrderLineGrosses</c>, because a
        /// customer must not be able to dispute a line an admin could never refund.
        /// </summary>
        private static HashSet<(string ServiceId, string? PackageId)> OrderLineIdentities(Order order)
        {
            var identities = new HashSet<(string, string?)>();

            foreach (var service in order.SelectedServices)
            {
                identities.Add((service.ServiceId, null));
            }

            foreach (var orderPackage in order.SelectedPackages)
            {
                foreach (var included in orderPackage.Package?.IncludedServices ?? [])
                {
                    identities.Add((included.ServiceId, orderPackage.PackageId));
                }
            }

            return identities;
        }

    }
}
