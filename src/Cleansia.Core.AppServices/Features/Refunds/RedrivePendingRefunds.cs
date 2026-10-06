using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Refunds;

/// <summary>
/// Every hour, re-drive the card refunds of cancelled orders that Stripe refused or could not be reached
/// for, each on the key it was created with, and tell the company's administrators about any refund still
/// owed and not through a day after it was asked for, saying whether this job retries it or they must
/// (owner ruling 2026-09-28). A refund with nothing left to give back is closed instead. A refund Stripe
/// made on the key and then failed is closed too, and raised for a retry at once, however young.
/// → /flows/cancellation-refund-dispute#refund
/// </summary>
public class RedrivePendingRefunds
{
    /// <param name="MinimumAgeMinutes">
    /// A refund younger than this may still be in flight with the request that created it; the shared key
    /// would make a race harmless at Stripe, but the first attempt deserves to finish.
    /// </param>
    /// <param name="AlertAfterHours">How long a refund may stay pending before the administrators are told.</param>
    public record Command(int MinimumAgeMinutes = 30, int AlertAfterHours = 24) : ICommand<Response>;

    public record Response(int Considered, int Redriven, int Alerted);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.MinimumAgeMinutes).InclusiveBetween(1, 1440);
            RuleFor(x => x.AlertAfterHours).InclusiveBetween(1, 720);
        }
    }

    /// <summary>No JWT on a sweep. Matches CancelUnfilledOrders and CleanupStalePendingOrders.</summary>
    private const string SystemActor = "system";

    public class Handler(
        IRefundRepository refundRepository,
        IRefundService refundService,
        INotificationProducer notificationProducer,
        IAdminNotifier adminNotifier,
        IUserNotificationRepository userNotificationRepository,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var youngest = now.AddMinutes(-command.MinimumAgeMinutes);
            var alertBefore = now.AddHours(-command.AlertAfterHours);

            // System job, no JWT: read across tenants, then set the override per company and commit
            // inside the loop, so every row is stamped with its own company. A chargeback row is the
            // bank's money movement, never ours to re-drive.
            var pending = await refundRepository.GetQueryableIgnoringTenant()
                .AsNoTracking()
                .Where(r => r.Status == RefundStatus.Pending
                    && r.Source == RefundSource.AppRefund
                    && r.CreatedOn <= youngest)
                .OrderBy(r => r.CreatedOn)
                .Select(r => new
                {
                    r.Id,
                    r.OrderId,
                    r.TenantId,
                    r.CreatedOn,
                    r.Amount,
                    r.Currency,
                    r.RefundKey,
                    r.Reason,
                    r.Order!.UserId,
                    r.Order.DisplayOrderNumber,
                    OrderStatus = r.Order.CurrentStatus,
                })
                .ToListAsync(cancellationToken);

            var redriven = 0;
            var alerted = 0;
            foreach (var tenantGroup in pending.GroupBy(r => r.TenantId ?? string.Empty))
            {
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                foreach (var row in tenantGroup)
                {
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
                    try
                    {
                        BusinessResult<RefundResult>? result = null;
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

                        var nothingOwed = result?.Error?.Message
                            is BusinessErrorMessage.RefundNothingRefundable
                            or BusinessErrorMessage.RefundOrderNotRefundable;

                        var failedAtStripe = result is { IsSuccess: false }
                            && result.Error?.Message == BusinessErrorMessage.RefundFailed
                            && (await refundRepository.GetByIdAsync(row.Id, cancellationToken))?.Status
                                == RefundStatus.Failed;
                        var raisedAs = failedAtStripe ? AdminNotificationEventCatalog.RefundNeedsRetry : alertKey;

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
                        else if (!nothingOwed && (row.CreatedOn <= alertBefore || failedAtStripe)
                            && !string.IsNullOrEmpty(row.TenantId)
                            && !await userNotificationRepository.AnyForEventAsync(
                                row.TenantId, raisedAs, "orderId", row.OrderId, cancellationToken))
                        {
                            await adminNotifier.NotifyAsync(
                                new AdminEvent(
                                    raisedAs,
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

                        await unitOfWork.CommitAsync(cancellationToken);
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        // One row that cannot be settled or recorded must not starve every row behind it,
                        // run after run, since the oldest row is always first. Its unsaved changes go so
                        // they cannot ride the next row's commit; the next run tries it again.
                        unitOfWork.Rollback();
                        logger.LogError(ex,
                            "Could not re-drive refund {RefundId} of order {OrderId}; the next run tries again",
                            row.Id, row.OrderId);
                        continue;
                    }

                    redriven += wentThrough ? 1 : 0;
                    alerted += raised ? 1 : 0;
                }
            }

            if (pending.Count > 0)
            {
                logger.LogInformation(
                    "RedrivePendingRefunds considered {Considered} pending refunds: {Redriven} re-driven, {Alerted} raised to administrators",
                    pending.Count, redriven, alerted);
            }

            return BusinessResult.Success(new Response(pending.Count, redriven, alerted));
        }
    }
}
