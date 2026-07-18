using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
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
    INotificationProducer notificationProducer,
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
        var thresholds = await ResolveThresholdsAsync(cancellationToken);
        account.GrantPoints(pointsEarned, LoyaltyEarnSource.OrderCompleted, orderId, SystemActor, thresholds);

        // Fire `loyalty.tier_upgrade` when this grant promotes the user. Detection is by snapshot
        // (previousTier captured pre-grant, post-grant compared) so the domain stays free of
        // notification concerns.
        // Only fires on UPGRADE — a revoke that demotes is silent (handled
        // by the cancellation push if relevant).
        if (account.CurrentTier > previousTier)
        {
            // The notification gates on the caller's (CompleteOrder's) commit: feed row + outbox row
            // are written into the shared scoped unit of work and become durable only if the grant
            // persists, so a rolled-back grant never records a phantom tier upgrade.
            await notificationProducer.NotifyAsync(
                order.UserId,
                NotificationEventCatalog.LoyaltyTierUpgrade,
                new Dictionary<string, string>
                {
                    ["tier"] = account.CurrentTier.ToString(),
                },
                order.TenantId,
                orderId,
                cancellationToken);
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

        var thresholds = await ResolveThresholdsAsync(cancellationToken);
        account.RevokePoints(pointsToRevoke, LoyaltyEarnSource.OrderCancelled, orderId, SystemActor, thresholds);
    }

    public async Task RevokeForPartialRefundAsync(
        string orderId, decimal refundNet, string refundKey, string actorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refundKey))
        {
            return;
        }

        var order = await orderRepository
            .GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
        {
            logger.LogWarning("LoyaltyService.PartialRevoke skipped — order {OrderId} not found.", orderId);
            return;
        }

        if (string.IsNullOrEmpty(order.UserId))
        {
            return;
        }

        var requested = (int)Math.Floor(refundNet / 10m);
        if (requested <= 0)
        {
            return;
        }

        // Idempotency on the refund key — a replay of the same partial refund collapses to one revoke.
        var existingByKey = await loyaltyTransactionRepository.GetByIdempotencyKeyAsync(refundKey, cancellationToken);
        if (existingByKey != null)
        {
            return;
        }

        var originalEarn = await loyaltyTransactionRepository.GetLatestForOrderSourceAsync(
            orderId, LoyaltyEarnSource.OrderCompleted, cancellationToken);
        if (originalEarn == null)
        {
            return;
        }

        // Cap cumulative revocation at the original earn so a near-full set of partial refunds can
        // never claw back more than was earned.
        var alreadyRevoked = await loyaltyTransactionRepository.GetRevokedPointsSumForOrderSourceAsync(
            orderId, LoyaltyEarnSource.OrderPartiallyRefunded, cancellationToken);
        var headroom = originalEarn.Points - alreadyRevoked;
        var pointsToRevoke = Math.Min(requested, headroom);
        if (pointsToRevoke <= 0)
        {
            return;
        }

        var account = await loyaltyAccountRepository.GetByUserIdAsync(order.UserId, cancellationToken);
        if (account == null)
        {
            return;
        }

        var thresholds = await ResolveThresholdsAsync(cancellationToken);
        account.RevokePoints(
            pointsToRevoke, LoyaltyEarnSource.OrderPartiallyRefunded, orderId, actorId, thresholds, idempotencyKey: refundKey);

        // Flush the keyed insert HERE so a concurrent double-submit that raced past the fast-path read
        // collides on the filtered unique index and collapses, rather than surfacing a raw 500 at the
        // pipeline commit (mirrors the manual grant/revoke path).
        await FlushCollapsingUniqueViolationAsync(cancellationToken);
    }

    public async Task<TierDiscountResult> ResolveTierDiscountForOrderAsync(
        string userId, decimal orderTotal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return new TierDiscountResult(0m, null);
        }

        var account = await loyaltyAccountRepository.GetByUserIdTierOnlyAsync(userId, cancellationToken);
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
        string? reason,
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

        // S7a — the admin manual path (orderId == null) is keyed on the
        // client-supplied requestId. Fast-path read: if a ledger row already carries this key, the
        // grant already landed (a double-submit / proxy-retry / network-retry) — no-op so points are
        // not doubled.
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
        var thresholds = await ResolveThresholdsAsync(cancellationToken);
        account.GrantPoints(points, source, orderId, actorId, thresholds, description: reason, idempotencyKey: requestId);

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
        string? reason,
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

        // S7a — admin manual path keyed on requestId. Fast-path collapse on a replay.
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

        var thresholds = await ResolveThresholdsAsync(cancellationToken);
        account.RevokePoints(points, source, orderId, actorId, thresholds, description: reason, idempotencyKey: requestId);

        // S7a/S7b backstop (see GrantPointsManuallyAsync): flush the keyed admin revoke so a concurrent
        // double-submit collapses on the filtered unique index instead of double-revoking.
        if (!string.IsNullOrEmpty(requestId))
        {
            await FlushCollapsingUniqueViolationAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Deliberate in-service flush of a keyed loyalty grant/revoke insert (the manual admin path and the
    /// partial-refund clawback) so a concurrent double-submit that raced past the fast-path read collides
    /// on the filtered UNIQUE INDEX on <c>LoyaltyTransaction.IdempotencyKey</c> HERE, where the 23505 can
    /// be caught and collapsed — not at the <c>UnitOfWorkPipelineBehavior</c> commit (which would surface a
    /// raw 500). On a unique-violation the loser rolls back ITS OWN change-tracker (each request has its own
    /// scoped DbContext, so this discards only the loser's row, never the winner's) and returns: the side
    /// effect lands exactly once. On a clean flush the row is persisted and Unchanged, so the pipeline's
    /// final commit is a safe no-op.
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
                "Keyed loyalty grant/revoke collapsed on idempotency key (unique-violation) — replay landed exactly once.");
        }
    }

    /// <summary>
    /// Builds the tenant's tier thresholds from the admin-editable <see cref="LoyaltyTierConfig"/> rows
    /// so tier resolution reads the configured values, not hardcoded literals. A missing tier row maps to
    /// <see cref="int.MaxValue"/> — unreachable, so resolution degrades to the next tier down rather than
    /// throwing (Bronze is the threshold-free floor).
    /// </summary>
    private async Task<LoyaltyTierThresholds> ResolveThresholdsAsync(CancellationToken cancellationToken)
    {
        var configs = await loyaltyTierConfigRepository.GetAllForTenantAsync(cancellationToken);
        return new LoyaltyTierThresholds(
            Silver: configs.FirstOrDefault(c => c.Tier == LoyaltyTier.SilverMopper)?.LifetimePointsThreshold ?? int.MaxValue,
            Gold: configs.FirstOrDefault(c => c.Tier == LoyaltyTier.GoldPolisher)?.LifetimePointsThreshold ?? int.MaxValue,
            Platinum: configs.FirstOrDefault(c => c.Tier == LoyaltyTier.PlatinumSparkler)?.LifetimePointsThreshold ?? int.MaxValue);
    }

    /// <summary>
    /// True when the <see cref="DbUpdateException"/> was caused by a Postgres unique-constraint
    /// violation (SQLSTATE 23505) — the filtered IdempotencyKey unique index rejecting a concurrent
    /// loser's insert. Detected provider-agnostically by duck-typing the inner exception's public
    /// <c>SqlState</c> property (the AppServices layer carries no hard Npgsql reference). Walks the whole
    /// inner chain because EF may wrap the provider exception more than one level deep.
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
