using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

[AuditAction("order.status.override", Sensitive = true, ResourceType = "Order")]
public class AdminOverrideOrderStatus
{
    public record Command(
        string OrderId,
        OrderStatus TargetStatus,
        // Kept on the audit row. Required to complete an order that has no after photo: the photo, or
        // a recorded administrator's exception (owner ruling 2026-09-28).
        string? Reason = null
    ) : ICommand<Response>;

    public record Response(
        string OrderId,
        OrderStatus Status);

    public record StatusSnapshot(string OrderId, OrderStatus? Status);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository, IOrderPhotoRepository orderPhotoRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.TargetStatus)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.Reason)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.Reason)
                .MustAsync(async (command, reason, cancellationToken) =>
                    !string.IsNullOrWhiteSpace(reason)
                    || await orderPhotoRepository.GetPhotoCountByOrderIdAndTypeAsync(
                        command.OrderId, PhotoType.After, cancellationToken) > 0)
                .WithMessage(BusinessErrorMessage.OrderForceCompleteReasonRequired)
                .When(x => x.TargetStatus == OrderStatus.Completed && !string.IsNullOrEmpty(x.OrderId));
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext,
        ILiveActivityProducer liveActivityProducer,
        IPendingDispatch pending,
        IReceivableRepository receivableRepository,
        INotificationProducer notificationProducer
    ) : ICommandHandler<Command, Response>
    {
        // The RANK array — not the set of legal targets. It must stay total over every status a row
        // can currently HOLD, Pending included: drop a member and Array.IndexOf returns -1 for a row
        // in that state, which satisfies `targetRank <= currentRank` for every target and inverts
        // the forward-only guard into a licence to walk backwards. Cancelled is absent because no
        // row reaches this code holding it (the terminal check above refuses first) and because
        // cancellation is AdminCancelOrder's, which carries the refund seam.
        private static readonly OrderStatus[] Lifecycle =
        [
            OrderStatus.New,
            OrderStatus.Pending,
            OrderStatus.Confirmed,
            OrderStatus.OnTheWay,
            OrderStatus.InProgress,
            OrderStatus.Completed,
        ];

        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            _ = userSessionProvider.GetUserId()!;
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                .Include(o => o.Receipts)
                .Include(o => o.Currency)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            var currentStatus = order.CurrentStatus;

            if (currentStatus == OrderStatus.Completed)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderAlreadyCompleted));
            }
            if (currentStatus == OrderStatus.Cancelled)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderAlreadyCancelled));
            }

            // OrderStatus.Pending is dead (ADR-0037 D5) — the state it names lives on the payment
            // axis (Card + PaymentStatus.Pending), which is what the live sweeps read. This generic
            // writer is the only way a new Pending row could appear, so it is refused here rather
            // than by removing the member from Lifecycle, which ranks legacy rows.
            if (command.TargetStatus == OrderStatus.Pending)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.TargetStatus),
                    BusinessErrorMessage.InvalidOrderStatusTransition));
            }

            var currentRank = Array.IndexOf(Lifecycle, currentStatus);
            var targetRank = Array.IndexOf(Lifecycle, command.TargetStatus);

            // A legal override is a strict forward move along the lifecycle. Same-state, backward,
            // and off-lifecycle targets (e.g. Cancelled) are ambiguous and never rewrite history.
            // An unrankable CURRENT status is refused too: unreachable while Lifecycle stays total,
            // which is exactly why it is written down — the next OrderStatus member added and
            // forgotten there would otherwise re-open the backwards move silently.
            if (currentRank < 0 || targetRank < 0 || targetRank <= currentRank)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.TargetStatus),
                    BusinessErrorMessage.InvalidOrderStatusTransition));
            }

            // Confirmed means a cleaner took the job, and a release that empties the crew walks it back
            // to New — so this is the one door an administrator could open onto that state with a
            // click. An administrator who wants a cleaner on the order reassigns, which writes
            // Confirmed itself. The other forward moves stay open on an unstaffed order: they repair
            // the work's state, not the crew's, and they are the administrator's own audited act.
            if (command.TargetStatus == OrderStatus.Confirmed && order.AssignedEmployees.Count == 0)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.TargetStatus),
                    BusinessErrorMessage.OrderStatusConfirmedNeedsCrew));
            }

            // The reports read CompletedAt, and CompleteOrder is not reached from here: an override that
            // completes the order has to date it or the order is revenue of no month.
            if (command.TargetStatus == OrderStatus.Completed)
            {
                order.MarkCompletedAt(DateTime.UtcNow);

                // A cash sale's receipt is issued at completion (owner ruling 2026-09-28), and this
                // completion does not pass through CompleteOrder. Uncollected cash earns none yet:
                // AdminRecordCashReceived issues it when the cash is recorded.
                if (order.SettledInCash && order.Receipt is null)
                {
                    pending.Enqueue(
                        QueueNames.GenerateReceipt,
                        new QueueEnvelope<GenerateReceiptMessage>(
                            MessageKeys.Receipt(order.Id),
                            order.TenantId,
                            new GenerateReceiptMessage(order.Id, Constants.Language.English)),
                        MessageKeys.Receipt(order.Id));
                }

                // The debt is the price of a cleaning done and not paid for (terms §8), so only an order in
                // progress opens it; from an earlier status the customer may have paid a cleaner who never
                // pressed Start. → /product/business-rules#cash-not-paid
                if (currentStatus == OrderStatus.InProgress
                    && order is { PaymentType: PaymentType.Cash, PaymentStatus: PaymentStatus.Pending, UserId: not null })
                {
                    await ReportCashNotPaid.OpenDoorDebtAsync(
                        order, receivableRepository, pending, notificationProducer, cancellationToken);
                }
            }

            var transition = OrderStatusTrack.Create(command.TargetStatus, order);
            order.AddOrderStatus(transition);

            // The override's FIRST notification-style call (ADR-0029 D2/RV-2): a state card must track
            // admin-driven forward moves. Forward-only, so Cancelled is unreachable here; a target with
            // no activity event (Confirmed) maps to null and produces nothing.
            var eventKey = LiveActivityEventKeys.ForStatus(command.TargetStatus);
            if (eventKey is not null)
            {
                await liveActivityProducer.NotifyOrderTransitionAsync(
                    order, eventKey, transition, cancellationToken);
            }

            auditContext.RecordChange(
                "Order",
                order.Id,
                new StatusSnapshot(order.Id, currentStatus),
                new StatusSnapshot(order.Id, command.TargetStatus),
                string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim());

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                Status: command.TargetStatus));
        }
    }
}
