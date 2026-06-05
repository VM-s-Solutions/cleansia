using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// Coordinates loyalty grant/revoke around order lifecycle events and resolves
/// tier-based discounts at booking time. Idempotent on grant/revoke so a
/// double-fire of CompleteOrder/CancelOrder is safe.
/// </summary>
public sealed class LoyaltyService(
    IOrderRepository orderRepository,
    ILoyaltyAccountRepository loyaltyAccountRepository,
    ILoyaltyTierConfigRepository loyaltyTierConfigRepository,
    ILoyaltyTransactionRepository loyaltyTransactionRepository,
    IQueueClient queueClient,
    ILogger<LoyaltyService> logger) : ILoyaltyService
{
    private const string SystemActor = "system";

    public async Task GrantForCompletedOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        var order = await orderRepository
            .GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            logger.LogWarning("LoyaltyService.Grant skipped — order {OrderId} not found.", orderId);
            return;
        }

        // Anonymous/legacy orders aren't tied to a user — skip silently.
        if (string.IsNullOrEmpty(order.UserId))
        {
            return;
        }

        var pointsEarned = (int)Math.Floor(order.TotalPrice / 10m);
        if (pointsEarned <= 0)
        {
            return;
        }

        // Idempotency: bail out if a prior Earn ledger entry exists for this
        // order. This protects against duplicate CompleteOrder fires (e.g.
        // pipeline retries, manual re-trigger).
        var existing = await loyaltyTransactionRepository.GetLatestForOrderSourceAsync(
            orderId, LoyaltyEarnSource.OrderCompleted, cancellationToken);
        if (existing != null)
        {
            return;
        }

        var account = await loyaltyAccountRepository.EnsureForUserAsync(order.UserId, cancellationToken);
        var previousTier = account.CurrentTier;
        account.GrantPoints(pointsEarned, LoyaltyEarnSource.OrderCompleted, orderId, SystemActor);

        // TASK-011a — fire `loyalty.tier_upgrade` when this grant promotes
        // the user. Detection is by snapshot (previousTier captured pre-grant,
        // post-grant compared) so domain stays free of notification concerns.
        // Only fires on UPGRADE — a revoke that demotes is silent (handled
        // by the cancellation push if relevant).
        if (account.CurrentTier > previousTier)
        {
            try
            {
                await queueClient.SendAsync(
                    QueueNames.NotificationsDispatch,
                    new SendPushNotificationMessage(
                        UserId: order.UserId,
                        EventKey: NotificationEventCatalog.LoyaltyTierUpgrade,
                        Args: new Dictionary<string, string>
                        {
                            ["tier"] = account.CurrentTier.ToString(),
                        },
                        TenantId: order.TenantId),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Loyalty grant already happened — don't roll it back over a
                // queue hiccup. Push is fail-soft; the user still sees the
                // new tier on their next app open via the Rewards screen.
                logger.LogWarning(ex,
                    "Failed to enqueue tier_upgrade push for user {UserId} (tier {PrevTier} → {NewTier})",
                    order.UserId, previousTier, account.CurrentTier);
            }
        }
    }

    public async Task RevokeForCancelledOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        var order = await orderRepository
            .GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            logger.LogWarning("LoyaltyService.Revoke skipped — order {OrderId} not found.", orderId);
            return;
        }

        if (string.IsNullOrEmpty(order.UserId))
        {
            return;
        }

        // Find the original Earn so we know how many points to walk back.
        var originalEarn = await loyaltyTransactionRepository.GetLatestForOrderSourceAsync(
            orderId, LoyaltyEarnSource.OrderCompleted, cancellationToken);
        if (originalEarn == null)
        {
            return;
        }

        // Idempotency: if a prior Revoke for this order already exists, no-op.
        var existingRevoke = await loyaltyTransactionRepository.GetLatestForOrderSourceAsync(
            orderId, LoyaltyEarnSource.OrderCancelled, cancellationToken);
        if (existingRevoke != null)
        {
            return;
        }

        var account = await loyaltyAccountRepository.GetByUserIdAsync(order.UserId, cancellationToken);
        if (account == null)
        {
            return;
        }

        // originalEarn.Points is the positive Earn magnitude.
        var pointsToRevoke = Math.Max(0, originalEarn.Points);
        if (pointsToRevoke == 0)
        {
            return;
        }

        account.RevokePoints(pointsToRevoke, LoyaltyEarnSource.OrderCancelled, orderId, SystemActor);
    }

    public async Task<TierDiscountResult> ResolveTierDiscountForOrderAsync(
        string userId, decimal orderTotal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return new TierDiscountResult(0m, null);
        }

        var account = await loyaltyAccountRepository.GetByUserIdAsync(userId, cancellationToken);
        if (account == null)
        {
            return new TierDiscountResult(0m, null);
        }

        var config = await loyaltyTierConfigRepository.GetByTierAsync(account.CurrentTier, cancellationToken);
        if (config == null || config.DiscountPercent <= 0m)
        {
            // Snapshot the tier even when no discount applies, so we can
            // render "Bronze Cleaner" on the receipt later.
            return new TierDiscountResult(0m, account.CurrentTier);
        }

        if (config.MinimumOrderAmountForDiscount.HasValue
            && orderTotal < config.MinimumOrderAmountForDiscount.Value)
        {
            return new TierDiscountResult(0m, account.CurrentTier);
        }

        var discount = Math.Round(orderTotal * config.DiscountPercent, 2, MidpointRounding.AwayFromZero);
        return new TierDiscountResult(discount, account.CurrentTier);
    }

    public async Task GrantPointsManuallyAsync(
        string userId,
        int points,
        LoyaltyEarnSource source,
        string? orderId,
        string actorId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId) || points <= 0)
        {
            return;
        }

        // Idempotency on (OrderId, Source) — same guard as the order-driven
        // grant, so a referral grant for a given orderId can only land once
        // even if ProcessOrderCompletedAsync is replayed.
        if (!string.IsNullOrEmpty(orderId))
        {
            var existing = await loyaltyTransactionRepository.GetLatestForOrderSourceAsync(
                orderId, source, cancellationToken);
            if (existing != null)
            {
                return;
            }
        }

        // T-0112 (LG-SEC-06 / S7a) — the admin manual path (orderId == null) is keyed on the
        // client-supplied requestId. Fast-path read: if a ledger row already carries this key, the
        // grant already landed (a double-submit / proxy-retry / network-retry) — no-op so points are
        // not doubled. The OLD ":194-198 intentional duplicates" assumption is REMOVED for this path.
        if (!string.IsNullOrEmpty(requestId))
        {
            var existingByKey = await loyaltyTransactionRepository.GetByIdempotencyKeyAsync(
                requestId, cancellationToken);
            if (existingByKey != null)
            {
                return;
            }
        }

        var account = await loyaltyAccountRepository.EnsureForUserAsync(userId, cancellationToken);
        account.GrantPoints(points, source, orderId, actorId, idempotencyKey: requestId);

        // S7a/S7b backstop: the fast-path read above is a TOCTOU optimization, not the guarantee. For
        // the keyed admin path, FLUSH the insert HERE so a concurrent double-submit that raced past the
        // read hits the filtered UNIQUE INDEX on IdempotencyKey and surfaces a Postgres 23505 WHERE we
        // can collapse it (per S7b: the UnitOfWorkPipelineBehavior commits AFTER the handler, so an
        // unhandled DbUpdateException would otherwise surface as a 500 at the pipeline). On the
        // violation the loser COLLAPSES: roll back its own tracked changes and return the same success.
        // After a clean flush the entity is Unchanged, so the pipeline's final commit is a safe no-op.
        if (!string.IsNullOrEmpty(requestId))
        {
            await FlushCollapsingUniqueViolationAsync(cancellationToken);
        }
    }

    public async Task RevokePointsManuallyAsync(
        string userId,
        int points,
        LoyaltyEarnSource source,
        string? orderId,
        string actorId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId) || points <= 0)
        {
            return;
        }

        // Idempotency mirror of GrantPointsManuallyAsync — order-driven (orderId, Source) guard.
        if (!string.IsNullOrEmpty(orderId))
        {
            var existing = await loyaltyTransactionRepository.GetLatestForOrderSourceAsync(
                orderId, source, cancellationToken);
            if (existing != null)
            {
                return;
            }
        }

        // T-0112 (S7a) — admin manual path keyed on requestId. Fast-path collapse on a replay.
        if (!string.IsNullOrEmpty(requestId))
        {
            var existingByKey = await loyaltyTransactionRepository.GetByIdempotencyKeyAsync(
                requestId, cancellationToken);
            if (existingByKey != null)
            {
                return;
            }
        }

        var account = await loyaltyAccountRepository.GetByUserIdAsync(userId, cancellationToken);
        if (account == null)
        {
            // Nothing to revoke against. Don't lazily create just to insert
            // a negative entry — that would leave the user with a phantom
            // zero-balance account.
            return;
        }

        account.RevokePoints(points, source, orderId, actorId, idempotencyKey: requestId);

        // S7a/S7b backstop (see GrantPointsManuallyAsync): flush the keyed admin revoke so a concurrent
        // double-submit collapses on the filtered unique index instead of double-revoking.
        if (!string.IsNullOrEmpty(requestId))
        {
            await FlushCollapsingUniqueViolationAsync(cancellationToken);
        }
    }

    /// <summary>
    /// T-0112 (S7b) — deliberate, documented in-service flush of the keyed manual grant/revoke insert so
    /// a concurrent double-submit that raced past the fast-path read collides on the filtered UNIQUE
    /// INDEX on <c>LoyaltyTransaction.IdempotencyKey</c> HERE, where the 23505 can be caught and
    /// collapsed — not at the <c>UnitOfWorkPipelineBehavior</c> commit (which would surface a raw 500).
    /// On a unique-violation the loser rolls back ITS OWN change-tracker (each request has its own scoped
    /// DbContext, so this discards only the loser's grant, never the winner's) and returns: the side
    /// effect lands exactly once and the admin sees the same success. On a clean flush the row is
    /// persisted and Unchanged, so the pipeline's final commit is a safe no-op.
    /// </summary>
    private async Task FlushCollapsingUniqueViolationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await loyaltyTransactionRepository.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The winner committed first — our keyed insert is a duplicate. Collapse: drop our tracked
            // changes so the pipeline commit won't retry the doomed insert, and return success-shaped
            // (the caller's handler returns the same Response either way).
            loyaltyTransactionRepository.Rollback();
            logger.LogInformation(
                "Manual loyalty grant/revoke collapsed on idempotency key (unique-violation) — replay landed exactly once.");
        }
    }

    /// <summary>
    /// True when the <see cref="DbUpdateException"/> was caused by a Postgres unique-constraint
    /// violation (SQLSTATE 23505) — the filtered IdempotencyKey unique index rejecting a concurrent
    /// loser's insert. Detected provider-agnostically by duck-typing the inner exception's public
    /// <c>SqlState</c> property (the AppServices layer carries no hard Npgsql reference). Walks the whole
    /// inner chain because EF may wrap the provider exception more than one level deep. Mirrors
    /// <c>CreateMembershipSubscription.Handler.IsUniqueViolation</c> (T-0111).
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        const string UniqueViolation = "23505";
        for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            var sqlState = inner.GetType()
                .GetProperty("SqlState")?
                .GetValue(inner) as string;
            if (sqlState == UniqueViolation)
            {
                return true;
            }
        }

        return false;
    }
}
