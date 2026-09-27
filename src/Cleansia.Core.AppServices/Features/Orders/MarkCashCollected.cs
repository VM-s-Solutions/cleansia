using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The assigned cleaner records that they collected the cash owed for an order that is not yet settled.
/// This flips the order to <see cref="PaymentStatus.Paid"/> (the same terminal payment state a
/// Stripe-charged card order reaches) and stamps who/when. It is the gate that lets an unsettled order
/// pass the CompleteOrder payment check.
/// <para>
/// It accepts a CARD booking too — a card order whose Stripe webhook never arrived is otherwise
/// impossible to complete in the field. For those, the handler reconciles against live Stripe first so
/// the customer is never asked to pay twice, and then takes cash only where the booking itself could
/// have chosen it (<see cref="BookingPolicy.AllowsCash"/>); <see cref="Order.PaymentType"/> stays as
/// booked and the tender actually taken is derived from the stamp via <see cref="Order.ActualPaymentType"/>.
/// An order booked as cash is collected as booked.
/// </para>
/// </summary>
public class MarkCashCollected
{
    public record Command(string OrderId) : ICommand<Response>;

    public record Response(string OrderId, PaymentStatus PaymentStatus);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IOrderAccessService _orderAccessService;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository,
            IOrderAccessService orderAccessService)
        {
            _orderRepository = orderRepository;
            _employeeRepository = employeeRepository;
            _orderAccessService = orderAccessService;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                // Cash only changes hands while the cleaner is on site, so collection is bounded to
                // InProgress for every payment type — the same window both mobile UIs already offer it in.
                .MustAsync(OrderIsInProgressAsync)
                .WithMessage(BusinessErrorMessage.OrderNotInProgress)
                .MustAsync(OrderIsNotAlreadyPaidAsync)
                .WithMessage(BusinessErrorMessage.OrderCashAlreadyCollected);

            // Same ownership gate as StartOrder / CompleteOrder: only an Approved cleaner assigned to the
            // order may collect its cash. Employee is server-derived from the caller (S1); empty caller
            // fails closed (S3 ownership).
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(EmployeeIsApprovedAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotApproved)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder);
        }

        private async Task<bool> OrderIsInProgressAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            return order?.CurrentStatus == OrderStatus.InProgress;
        }

        private async Task<bool> OrderIsNotAlreadyPaidAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            return order is not null && order.PaymentStatus != PaymentStatus.Paid;
        }

        private async Task<bool> EmployeeIsApprovedAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
            return employee?.ContractStatus == ContractStatus.Approved;
        }

        private async Task<bool> EmployeeIsAssignedToOrderAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId) ?? false;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService,
        IStripeClient stripeClient,
        IPendingDispatch pending,
        ILogger<Handler> logger)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.Receipt)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // FAIL CLOSED on a credit-bearing order, for the same reason this handler already fails
            // closed on an unreadable Stripe: it names no amount, and nothing the cleaner can see names
            // one either. The job sheet carries TotalPrice, but on an order settled partly from the
            // customer's credit balance TotalPrice is not what is owed at the door - the credit was
            // already spent, and collecting the full figure charges the customer for it twice.
            //
            // Refusing is the smaller and safer half of the choice: an admin can still drive the job
            // forward with AdminOverrideOrderStatus, whereas a cleaner who has already taken the wrong
            // amount in cash cannot be undone from here. -> Order.AmountDueOnCard
            if (order.CreditAppliedAmount > 0m)
            {
                logger.LogWarning(
                    "Refusing cash collection on order {OrderId}: {Credit} of it was settled from the "
                    + "customer's credit balance, and the cash figure on the job sheet is the full sale.",
                    order.Id, order.CreditAppliedAmount);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentStatus), BusinessErrorMessage.CashNotCollectableOnCreditOrder));
            }

            string? outstandingIntentId = null;
            if (order.HasRefundableChargeSurface)
            {
                var (rejection, outstanding) = await ReconcileCardSurfaceAsync(order, cancellationToken);
                if (rejection is not null)
                {
                    return rejection;
                }

                outstandingIntentId = outstanding;
            }

            // Here and not in the validator: it must follow the Stripe repair above, or a settled card
            // order the rule refuses could never reach Paid; and it must precede the cancel below, or a
            // refusal would leave the customer's card intent closed and the order payable by nothing.
            if (order.PaymentType != PaymentType.Cash
                && !BookingPolicy.AllowsCash(!string.IsNullOrEmpty(order.UserId), order.RequiredEmployees))
            {
                logger.LogWarning(
                    "Refusing cash collection on card order {OrderId}: its booking could not have been paid in cash",
                    order.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentType), BusinessErrorMessage.OrderCashNotAllowedOnCardOrder));
            }

            if (!string.IsNullOrEmpty(outstandingIntentId))
            {
                await CancelOutstandingIntentAsync(order, outstandingIntentId, cancellationToken);
            }

            // The validator guarantees an Approved, assigned caller, so the employee id is present.
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            order.MarkCashCollected(employeeId!);

            // A cash sale's receipt was issued at booking, before any money moved, so it says the sale is
            // awaiting payment. Staged as intent, so only a committed collection restates it.
            if (order.Receipt is not null)
            {
                pending.Enqueue(
                    QueueNames.GenerateReceipt,
                    new QueueEnvelope<GenerateReceiptMessage>(
                        MessageKeys.ReceiptReissue(order.Id),
                        order.TenantId,
                        new GenerateReceiptMessage(order.Id, LanguageCode: string.Empty, Reissue: true)),
                    MessageKeys.ReceiptReissue(order.Id));
            }

            return BusinessResult.Success(new Response(order.Id, order.PaymentStatus));
        }

        /// <summary>
        /// Asks Stripe what actually happened on the order's charge surface before a second tender is
        /// taken. Returns the rejection to surface, or none plus the intent still open at Stripe. Fails
        /// CLOSED on an unreachable Stripe: telling a customer to pay cash for a charge that may already
        /// have settled costs real money, and an admin can still drive the job forward with
        /// <see cref="AdminOverrideOrderStatus"/>.
        /// </summary>
        private async Task<(BusinessResult<Response>? Rejection, string? OutstandingIntentId)> ReconcileCardSurfaceAsync(
            Order order, CancellationToken cancellationToken)
        {
            StripePaymentSnapshot snapshot;
            try
            {
                snapshot = await stripeClient.GetPaymentSnapshotAsync(
                    order.StripeSessionId, order.StripePaymentIntentId, cancellationToken);
            }
            catch (Exception ex) when (IsStripeReadFailure(ex, cancellationToken))
            {
                logger.LogError(ex,
                    "Could not read the Stripe payment state for order {OrderId}; refusing the cash collection to avoid a double charge",
                    order.Id);
                return (BusinessResult.Failure<Response>(new Error(
                    nameof(order.PaymentStatus), BusinessErrorMessage.CardPaymentUnverified)), null);
            }

            switch (snapshot.State)
            {
                case StripePaymentState.Settled:
                    order.UpdatePaymentStatus(PaymentStatus.Paid);
                    // The UnitOfWork pipeline commits only SUCCESSFUL commands, so this repair has to be
                    // flushed here or the cleaner refreshes back into the same dead end forever.
                    await orderRepository.CommitAsync(cancellationToken);
                    logger.LogWarning(
                        "Order {OrderId} was already settled at Stripe; repaired the payment status instead of collecting cash",
                        order.Id);
                    return (BusinessResult.Failure<Response>(new Error(
                        nameof(order.PaymentStatus), BusinessErrorMessage.CardPaymentAlreadySettled)), null);

                case StripePaymentState.Processing:
                    logger.LogWarning(
                        "Order {OrderId} has a card payment in flight at Stripe; refusing the cash collection",
                        order.Id);
                    return (BusinessResult.Failure<Response>(new Error(
                        nameof(order.PaymentStatus), BusinessErrorMessage.CardPaymentInProgress)), null);
            }

            return (null, snapshot.OutstandingPaymentIntentId);
        }

        /// <summary>
        /// Best-effort: closing the intent stops the customer paying the card later on top of the cash.
        /// A failure here is not worth blocking the collection — the late-arrival webhook path raises a
        /// dispute for a human if it does settle after all.
        /// </summary>
        private async Task CancelOutstandingIntentAsync(
            Order order, string outstandingIntentId, CancellationToken cancellationToken)
        {
            try
            {
                await stripeClient.CancelPaymentIntentAsync(outstandingIntentId, cancellationToken);
            }
            catch (Exception ex) when (IsStripeReadFailure(ex, cancellationToken))
            {
                logger.LogWarning(ex,
                    "Could not cancel the outstanding PaymentIntent for order {OrderId} before recording cash",
                    order.Id);
            }
        }

        // A caller-requested cancellation is a genuine abort, not a Stripe outage — never launder it
        // into a business failure.
        private static bool IsStripeReadFailure(Exception ex, CancellationToken cancellationToken) =>
            ex is StripeException or HttpRequestException or TimeoutException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);
    }
}
