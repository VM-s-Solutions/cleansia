using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

[AuditAction("customer.order.cancel", Audience = AuditAudience.Customer, ResourceType = "Order")]
public class CancelOrder
{
    public record Command(
        string OrderId,
        string? Reason
    ) : ICommand<Response>;

    /// <param name="RefundAmount">What goes back to the customer: zero on an order that took no payment.</param>
    /// <param name="RefundPending">
    /// A card refund was owed and Stripe did not take it; it is re-driven hourly, so the customer is told
    /// the refund is on its way.
    /// </param>
    public record Response(
        string OrderId,
        decimal FeeRate,
        decimal RefundAmount,
        decimal TotalPrice,
        bool RefundInitiated,
        decimal? ActualRefundAmount = null,
        bool RefundPending = false);

    /// <summary>
    /// What the cancel cost and why, as the server computed it at the click (ADR-0062 D3). The reason
    /// text stays on the order (<c>Order.CancellationReason</c>, or <c>Order.CustomerCancellationReason</c>
    /// beside a platform reason key); the row records only that one was given.
    /// </summary>
    public record OrderCancellationEvidence(
        CancellationFeeTier Tier,
        decimal FeeRate,
        decimal FeeAmount,
        decimal RefundAmount,
        decimal TotalPrice,
        string CurrencyId,
        bool HasBeenAccepted,
        decimal HoursBeforeCleaning,
        decimal MinutesSinceBooking,
        int FreeCancellationHoursApplied,
        int OopsMinutesApplied,
        OopsWindowRule OopsRuleApplied,
        CancellationPolicyFigures PolicyFigures,
        bool ExpressWaiverReleased,
        bool RefundInitiated,
        PaymentType PaymentType,
        PaymentStatus PaymentStatus,
        bool ReasonProvided,
        decimal? ActualRefundAmount = null) : ICustomerAuditPayload;

    /// <summary>The schedule the cancel was priced under: the order's frozen figures and today's oops windows.</summary>
    public record CancellationPolicyFigures(
        int FreeHours,
        int PartialHours,
        decimal PartialRate,
        decimal LastMinuteRate,
        int OopsMinutesStandard,
        int OopsMinutesPlus,
        int OopsMinutesFirstBooking)
    {
        public static CancellationPolicyFigures Of(Order order) => new(
            order.CancellationFreeHours,
            order.CancellationPartialHours,
            order.CancellationPartialFeeRate,
            order.CancellationLastMinuteFeeRate,
            BookingPolicy.OopsWindowMinutesStandard,
            BookingPolicy.OopsWindowMinutesPlus,
            BookingPolicy.OopsWindowMinutesFirstBooking);
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.Reason)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider,
        CustomerOrderCancellation cancellation,
        CleanerNoShowCancellation noShowCancellation,
        GuestOrderAccessTokenIssuer accessTokenIssuer,
        IPendingDispatch pending
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            // Missing and foreign orders share the handler's refusal and failure-audit path. The apology
            // credit of a booking nobody came to is read off its currency.
            var order = await orderAccessService
                .OrdersListedForCaller()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(ae => ae.Employee)
                .Include(o => o.Currency)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            var listedGuestBooking = orderAccessService.IsListedGuestBooking(order);
            if (order.UserId != userId && !listedGuestBooking)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            if (cancellation.BlockedReason(order) is { } blockedReason)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    blockedReason));
            }

            // A booking made without an account is cancelled on its own guest terms, as its e-mailed link
            // cancels it, whoever opens it, and before the no-cleaner body, which pays an account.
            if (listedGuestBooking)
            {
                var guestOutcome = await cancellation.ExecuteAsync(order, command.Reason, userId, cancellationToken);
                await GuestCancellationEmail.EnqueueAsync(order, languageCode: null, guestOutcome.SuccessfulRefundAmount,
                    accessTokenIssuer, pending, cancellationToken);
                return BusinessResult.Success(guestOutcome.Response);
            }

            var result = cancellation.IsUnfilledAtItsStart(order)
                ? await cancellation.ExecuteAsNoCleanerAsync(
                    order, command.Reason, userId, noShowCancellation, cancellationToken)
                : await cancellation.ExecuteAsync(order, command.Reason, userId, cancellationToken);
            return BusinessResult.Success(result.Response);
        }
    }
}
