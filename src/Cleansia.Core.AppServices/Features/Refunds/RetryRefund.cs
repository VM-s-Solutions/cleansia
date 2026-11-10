using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Refunds.Mappers;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Refunds;

/// <summary>
/// An administrator retries one refund Stripe has not confirmed, on its own key: the seam asks Stripe first and
/// sends only if Stripe has nothing, so it is never paid twice. → /flows/cancellation-refund-dispute#refund
/// </summary>
[AuditAction("order.refund.retry", Sensitive = true, ResourceType = "Order")]
public class RetryRefund
{
    public record Command(string OrderId, string RefundId) : ICommand<Response>;

    public record Response(string RefundId, Code State);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.RefundId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IRefundRepository refundRepository,
        IOrderRepository orderRepository,
        IRefundService refundService,
        ILoyaltyService loyaltyService,
        IUserSessionProvider userSessionProvider,
        INotificationProducer notificationProducer,
        IOutboxMessageRepository outboxMessageRepository,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var adminId = userSessionProvider.GetUserId()!;
            var refund = await refundRepository.GetByIdAsync(command.RefundId, cancellationToken);
            var order = refund?.OrderId == command.OrderId
                ? await orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
                : null;
            if (refund is null || order is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.RefundId), BusinessErrorMessage.RefundNotFound));
            }

            if (!refund.CanRetry(order.CurrentStatus))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.RefundId), BusinessErrorMessage.RefundNotRetryable));
            }

            BusinessResult<RefundResult> result;
            try
            {
                result = await refundService.RedriveAsync(refund.Id, adminId, cancellationToken);
            }
            catch (Exception ex) when (RefundService.IsStripeTransportFailure(ex, cancellationToken))
            {
                logger.LogWarning(ex, "Could not reach Stripe to retry refund {RefundId} of order {OrderId}", refund.Id, order.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.RefundId), BusinessErrorMessage.RefundFailed));
            }

            // A refund the seam closed is answered as closed, so the pipeline commits the close it staged.
            if (result.IsFailure)
            {
                return refund.MapToState() == RefundState.NotSent
                    ? BusinessResult.Failure<Response>(new Error(nameof(command.RefundId), BusinessErrorMessage.RefundFailed))
                    : BusinessResult.Success(new Response(refund.Id, refund.MapToState().MapToCode()));
            }

            var settled = result.Value!;
            var origin = refund.MapToOrigin();
            if (origin is RefundOrigin.Cancellation or RefundOrigin.FullRefund
                && !string.IsNullOrEmpty(order.UserId)
                && await outboxMessageRepository.GetByQueueAndKeyAsync(
                    QueueNames.NotificationsDispatch,
                    MessageKeys.Push(order.UserId, NotificationEventCatalog.OrderRefunded, settled.RefundId),
                    cancellationToken) is null)
            {
                await notificationProducer.NotifyAsync(
                    order.UserId,
                    NotificationEventCatalog.OrderRefunded,
                    new Dictionary<string, string>
                    {
                        ["orderId"] = order.Id,
                        ["orderNumber"] = order.DisplayOrderNumber,
                    },
                    order.TenantId,
                    settled.RefundId,
                    cancellationToken);
            }

            // Last: the clawback flushes the unit of work. A cancellation's points went with the cancel itself.
            if (origin != RefundOrigin.Cancellation)
            {
                await loyaltyService.RevokeForRefundAsync(
                    order.Id,
                    origin == RefundOrigin.FullRefund ? order.TotalPrice : settled.Amount + settled.CreditReturned,
                    settled.RefundKey,
                    adminId,
                    cancellationToken);
            }

            return BusinessResult.Success(new Response(refund.Id, RefundState.Refunded.MapToCode()));
        }
    }
}
