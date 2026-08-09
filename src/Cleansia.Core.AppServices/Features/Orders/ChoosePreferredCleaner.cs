using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// ADR-0045 D5.1 — the customer's second and final choice, offered at the lapse and never before it.
/// It is the SAME decision as at booking and therefore runs the SAME resolver against the EXISTING
/// order: membership, cleaner eligibility, work country, reachability, the ADR-0039 slot check and the
/// lead-time floor are all its answer, evaluated against the order's own persisted
/// <c>CleaningDateTime</c> and <c>EstimatedTime</c> — no client input decides a server answer (S1).
/// "The same flow of approval" is satisfied by construction: there is one resolver and it is the one
/// <c>OrderFactory</c> calls.
///
/// <para>Every structural refusal collapses to <c>order.preferred_offer_closed</c> (ADR-0036 D5.2 —
/// never introduce an error key that names the exclusivity). A per-reason code would let the customer
/// tell "someone else is still considering it" from "your favourite passed", which is exactly what no
/// surface may say.</para>
///
/// <para>The customer's second choice becomes available when the reservation ENDS, not before: a live
/// beneficiary was told the job was theirs and cannot be evicted by the other party. The exit is not
/// the customer's only way out — cancelling during a reservation is free throughout, because no
/// assignment row exists.</para>
/// </summary>
public class ChoosePreferredCleaner
{
    public record Command(string OrderId, string EmployeeId) : ICommand<Response>;

    public record Response(string OrderId, DateTime RespondByUtc, int Round);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserSessionProvider _userSessionProvider;
        private readonly IUserMembershipRepository _userMembershipRepository;
        private readonly IOrderRepository _orderRepository;

        public Validator(
            IUserSessionProvider userSessionProvider,
            IUserMembershipRepository userMembershipRepository,
            IOrderRepository orderRepository)
        {
            _userSessionProvider = userSessionProvider;
            _userMembershipRepository = userMembershipRepository;
            _orderRepository = orderRepository;

            RuleFor(x => x.OrderId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            // The same two entitlement gates CreateOrder.Validator applies to a preferred cleaner, in
            // the same order and with the same two keys. ONE ordered chain, never a second RuleFor: the
            // class-level default is Continue, so a parallel chain would report both refusals.
            // Entitlement leads because it reveals least — it does not depend on the employee id at
            // all, so a caller without Plus learns nothing about anyone by probing.
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(command => !string.IsNullOrWhiteSpace(command.EmployeeId))
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(CallerHasActiveMembershipAsync)
                .WithMessage(BusinessErrorMessage.PreferredEmployeeMembershipRequired)
                .MustAsync(PreferredEmployeeIsEligibleAsync)
                .WithMessage(BusinessErrorMessage.PreferredEmployeeNotEligible)
                .WithName(nameof(Command.EmployeeId));
        }

        private async Task<bool> CallerHasActiveMembershipAsync(
            Command command, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();

            return !string.IsNullOrEmpty(userId)
                && await _userMembershipRepository
                    .GetActiveForUserNoTrackingAsync(userId, cancellationToken) is not null;
        }

        private async Task<bool> PreferredEmployeeIsEligibleAsync(
            Command command, CancellationToken cancellationToken)
            => await _orderRepository.UserHasCompletedOrderWithEmployeeAsync(
                _userSessionProvider.GetUserId()!, command.EmployeeId, cancellationToken);
    }

    public class Handler(
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider,
        IPreferredCleanerHoldResolver preferredCleanerHoldResolver,
        INotificationProducer notificationProducer) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var userId = userSessionProvider.GetUserId();

            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .Include(o => o.CustomerAddress)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null || string.IsNullOrEmpty(userId) || order.UserId != userId)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // The read side of this same call is the order detail's canChooseAnother, so a button the
            // client offers and a request the server accepts cannot disagree.
            if (!PreferredOfferExit.IsOpen(order, nowUtc)
                || order.PreferredEmployeeId == command.EmployeeId)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.EmployeeId), BusinessErrorMessage.PreferredOfferClosed));
            }

            var resolved = await preferredCleanerHoldResolver.ResolveAsync(
                userId,
                command.EmployeeId,
                order.CustomerAddress?.CountryId,
                order.CleaningDateTime,
                order.EstimatedTime,
                nowUtc,
                cancellationToken);

            // A NotifyOnly outcome is REFUSED rather than granted-and-burned. A re-offer that cannot
            // withhold a seat is not a reservation: it would push a named cleaner about an order the
            // whole board already holds, burn the customer's final round, leave the state at None, and
            // then produce a closure message on the next sweep tick for a reservation that never was.
            if (resolved.HoldUntilUtc is not { } holdUntilUtc || resolved.Recipient is not { } recipient)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.EmployeeId), BusinessErrorMessage.PreferredOfferClosed));
            }

            order.GrantPreferredHold(
                command.EmployeeId, holdUntilUtc, nowUtc, BookingPolicy.MaxPreferredOfferRounds);

            await notificationProducer.NotifyAsync(
                recipient.UserId,
                NotificationEventCatalog.PreferredOffer,
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id,
                    ["orderNumber"] = order.DisplayOrderNumber,
                },
                recipient.TenantId,
                order.Id,
                cancellationToken);

            return BusinessResult.Success(
                new Response(order.Id, holdUntilUtc, order.PreferredOfferRound));
        }
    }
}
