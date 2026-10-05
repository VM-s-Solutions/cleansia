using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// An administrator confirms the assigned cleaner's report that they could not get in (owner ruling
/// 2026-09-28, decisions 11 and 13), also on a job the cleaner already started at the door: the booking is
/// cancelled as the customer's, at the whole price. A card order keeps its payment and the credit applied
/// to it, and a guest is never charged beyond the prepayment; a signed-in customer's cash booking that took
/// no payment gets its credit back and owes the price as a lockout receivable. Each seat is paid its full
/// reward at once, whether or not that price is ever paid (owner decision 2026-10-04).
/// → /product/business-rules#cancellation
/// </summary>
[AuditAction("order.cancel.lockout", ResourceType = "Order")]
public class AdminCancelOrderAsLockout
{
    public record Command(string OrderId) : ICommand<Response>;

    /// <param name="FeeAmount">The whole price, kept from the payment or owed.</param>
    /// <param name="ReceivableAmount">What the customer now owes on a cash booking, or null.</param>
    public record Response(string OrderId, decimal FeeAmount, decimal? ReceivableAmount);

    public record LockoutSnapshot(
        OrderStatus Status,
        PaymentType PaymentType,
        PaymentStatus PaymentStatus,
        decimal TotalPrice,
        decimal CreditAppliedAmount,
        decimal? CancellationFeeRate,
        decimal? ReceivableAmount,
        DateTime? LockoutReportedAt,
        string? LockoutReportedByEmployeeId);

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
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IReceivableRepository receivableRepository,
        ICreditAccountRepository creditAccountRepository,
        IUserSessionProvider userSessionProvider,
        INotificationProducer notificationProducer,
        ILiveActivityProducer liveActivityProducer,
        ILoyaltyService loyaltyService,
        GuestOrderAccessTokenIssuer guestAccessTokenIssuer,
        IPendingDispatch pending,
        IAuditContext auditContext,
        TimeProvider timeProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var adminId = userSessionProvider.GetUserId()!;
            var order = await orderRepository
                .GetQueryable()
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

            if (RefusalFor(order) is { } refusal)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), refusal));
            }

            var before = Snapshot(order, receivableAmount: null);
            order.Cancel(
                timeProvider.GetUtcNow().UtcDateTime,
                CancelledBy.Admin,
                feeRate: BookingPolicy.LockoutFeeRate,
                refundAmount: 0m,
                reason: OrderCancellationReasons.CustomerLockout);
            var transition = OrderStatusTrack.Create(OrderStatus.Cancelled, order);
            order.AddOrderStatus(transition);
            await liveActivityProducer.NotifyOrderTransitionAsync(
                order, LiveActivityEventKeys.End, transition, cancellationToken);

            var guest = string.IsNullOrEmpty(order.UserId);
            decimal? receivableAmount = null;
            if (order.PaymentType == PaymentType.Cash && order.TookNoPayment)
            {
                await creditAccountRepository.ReturnUnpaidOrderCreditAsync(order, cardRefunded: 0m, adminId, cancellationToken);
                if (!guest && order.TotalPrice > 0m)
                {
                    receivableRepository.Add(Receivable.ForLockout(order, order.TotalPrice));
                    receivableAmount = order.TotalPrice;
                }
            }

            CalculateOrderPay.EnqueueForCrew(order, pending);

            await OrderAssignmentCancellationNotifier.NotifyAssignedEmployeesOfCancellationAsync(
                order, notificationProducer, cancellationToken);
            await loyaltyService.RevokeForCancelledOrderAsync(order.Id, cancellationToken);
            await TellCustomerAsync(order, guest, cancellationToken);

            auditContext.RecordChange("Order", order.Id, before, Snapshot(order, receivableAmount));

            return BusinessResult.Success(new Response(order.Id, order.TotalPrice, receivableAmount));
        }

        private static string? RefusalFor(Order order) => order.CurrentStatus switch
        {
            OrderStatus.Cancelled => BusinessErrorMessage.OrderAlreadyCancelled,
            OrderStatus.Completed => BusinessErrorMessage.OrderAlreadyCompleted,
            _ when order.LockoutReportedAt is null => BusinessErrorMessage.LockoutNotReported,
            _ => null,
        };

        private async Task TellCustomerAsync(Order order, bool guest, CancellationToken cancellationToken)
        {
            if (guest)
            {
                await GuestCancellationEmail.EnqueueAsync(order, languageCode: null, successfulRefundAmount: null,
                    guestAccessTokenIssuer, pending, cancellationToken);
                return;
            }

            await notificationProducer.NotifyAsync(
                order.UserId!,
                NotificationEventCatalog.OrderCancelled,
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id,
                    ["orderNumber"] = order.DisplayOrderNumber,
                },
                order.TenantId,
                order.Id,
                cancellationToken);

            var key = MessageKeys.OrderLockoutEmail(order.Id);
            pending.Enqueue(QueueNames.SendEmail,
                new QueueEnvelope<SendOrderLockoutEmailMessage>(key, order.TenantId,
                    new SendOrderLockoutEmailMessage(order.Id, EmailLocale.Resolve(order.LanguageCode), order.TenantId)),
                key);
        }

        private static LockoutSnapshot Snapshot(Order order, decimal? receivableAmount) => new(
            order.CurrentStatus,
            order.PaymentType,
            order.PaymentStatus,
            order.TotalPrice,
            order.CreditAppliedAmount,
            order.CancellationFeeRate,
            receivableAmount,
            order.LockoutReportedAt,
            order.LockoutReportedByEmployeeId);
    }
}
