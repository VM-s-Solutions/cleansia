using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Refunds;

/// <summary>One refund in the sweep's own child scope; the pipeline owns its final commit. → /decisions/adr-0072</summary>
public class RedrivePendingRefund
{
    public record Command(string RefundId, DateTimeOffset Youngest, DateTimeOffset AlertBefore) : ICommand<Response>;
    public record Response(bool Redriven, bool Alerted);

    public class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.RefundId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
    }

    private const string SystemActor = "system";

    public class Handler(
        IRefundRepository refundRepository,
        IRefundService refundService,
        INotificationProducer notificationProducer,
        IAdminNotifier adminNotifier,
        IUserNotificationRepository userNotificationRepository,
        ITenantProvider tenantProvider,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var row = await refundRepository.GetQueryableIgnoringTenant()
                .AsNoTracking()
                .Where(r => r.Id == command.RefundId)
                .Select(r => new
                {
                    r.Id, r.OrderId, r.TenantId, r.CreatedOn, r.Amount, r.Currency, r.RefundKey, r.Reason,
                    r.Status, r.Source,
                    r.Order!.UserId, r.Order.DisplayOrderNumber,
                    OrderTenantId = r.Order.TenantId,
                    OrderStatus = r.Order.CurrentStatus,
                    CardCharged = r.Order.TotalPrice - r.Order.CreditAppliedAmount,
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (row is null || row.Status != RefundStatus.Pending || row.Source != RefundSource.AppRefund
                || row.CreatedOn > command.Youngest || string.IsNullOrEmpty(row.TenantId)
                || row.OrderTenantId != row.TenantId)
                return BusinessResult.Success(new Response(false, false));

            tenantProvider.ClearTenantOverride();
            tenantProvider.SetTenantOverride(row.TenantId);
            // Only a cancellation's own refund, on the order it cancelled, is finished here: its
            // cancel committed before the money was asked for, and nothing follows the money. A
            // guest's refund is claimed BEFORE the cancel and outlives a cancel that failed: re-driving
            // it would refund a clean that still happens, yet a timeout may come after Stripe paid it,
            // and every later refund on the order counts it as owed. Only cancelling the booking retries
            // it, so the administrators are told to do that if it is still open, or to reconcile the claim
            // in Stripe if it went ahead. A dispute's, an admin's or a partial refund carries a
            // distinguishing key segment and belongs to the action that asked for it, whose retry runs its
            // own follow-up; the administrators are told to check it in Stripe and retry it there.
            var cancellationsOwnKey = row.RefundKey == RefundService.BuildRefundKey(
                new RefundRequest(row.OrderId, row.Amount, row.Reason, SystemActor));
            var redrive = cancellationsOwnKey && row.OrderStatus == OrderStatus.Cancelled;

            var alertKey = redrive ? AdminNotificationEventCatalog.RefundStuck
                : cancellationsOwnKey ? AdminNotificationEventCatalog.RefundWithoutCancel
                : AdminNotificationEventCatalog.RefundNeedsRetry;
            var wentThrough = false;
            var raised = false;
            BusinessResult<RefundResult>? result = null;
            var closed = false;
            if (redrive)
            {
                try
                {
                    result = await refundService.RedriveAsync(row.Id, SystemActor, cancellationToken);
                }
                catch (Exception ex) when (RefundService.IsStripeTransportFailure(ex, cancellationToken))
                {
                    logger.LogWarning(ex,
                        "Could not reach Stripe to re-drive refund {RefundId} of order {OrderId}",
                        row.Id, row.OrderId);
                }
            }
            else if (row.CardCharged - await refundRepository.GetSucceededRefundTotalForOrderAsync(
                         row.OrderId, cancellationToken) <= 0m)
            {
                // Stripe never refunds more than the charge, so once the refunds it confirmed took all
                // of it this row cannot have been paid. The total is read before the row so a row
                // settled in between is seen settled and left alone.
                var refund = await refundRepository.GetByIdAsync(row.Id, cancellationToken);
                if (refund is { Status: RefundStatus.Pending })
                {
                    refund.MarkFailed();
                    logger.LogInformation(
                        "Refund {RefundId} of order {OrderId} closed: refunds Stripe confirmed took the whole card charge",
                        row.Id, row.OrderId);
                }

                closed = true;
            }

            var nothingOwed = closed
                || result?.Error?.Message
                    is BusinessErrorMessage.RefundNothingRefundable
                    or BusinessErrorMessage.RefundOrderNotRefundable;

            // Stripe failed it: the refund seam closed it and raised it for a retry in a commit of its own.
            var raisedBySeam = result is { IsSuccess: false }
                && result.Error?.Message == BusinessErrorMessage.RefundFailed
                && (await refundRepository.GetByIdAsync(row.Id, cancellationToken))?.Status
                    == RefundStatus.Failed;

            if (result is { IsSuccess: true })
            {
                wentThrough = true;
                // Resolved to a row somebody else settled in the meantime; they told the customer.
                if (!result.Value!.ResolvedToExisting && !string.IsNullOrEmpty(row.UserId))
                {
                    await notificationProducer.NotifyAsync(
                        row.UserId,
                        NotificationEventCatalog.OrderRefunded,
                        new Dictionary<string, string>
                        {
                            ["orderId"] = row.OrderId,
                            ["orderNumber"] = row.DisplayOrderNumber,
                        },
                        row.TenantId,
                        // The refund, not the order: an order can see more than one refund.
                        row.Id,
                        cancellationToken);
                }
            }
            else if (!nothingOwed && !raisedBySeam && row.CreatedOn <= command.AlertBefore
                && !string.IsNullOrEmpty(row.TenantId)
                && !await userNotificationRepository.AnyForEventIgnoringTenantAsync(
                    row.TenantId, alertKey, "orderId", row.OrderId, cancellationToken))
            {
                await adminNotifier.NotifyAsync(
                    new AdminEvent(
                        alertKey,
                        row.TenantId,
                        Subject: row.Id,
                        Args: new Dictionary<string, string>
                        {
                            ["orderNumber"] = row.DisplayOrderNumber,
                            ["amount"] = MoneyText.Format(row.Amount, row.Currency),
                            ["orderId"] = row.OrderId,
                        }),
                    cancellationToken);
                raised = true;
            }

            return BusinessResult.Success(new Response(wentThrough, raised));
        }
    }
}
