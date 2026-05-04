using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
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
        account.GrantPoints(pointsEarned, LoyaltyEarnSource.OrderCompleted, orderId, SystemActor);
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
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId) || points <= 0)
        {
            return;
        }

        // Idempotency on (OrderId, Source) — same guard as the order-driven
        // grant, so a referral grant for a given orderId can only land once
        // even if ProcessOrderCompletedAsync is replayed. Manual grants
        // without an order id (admin gifts) skip the check — those are
        // intentional duplicates by definition.
        if (!string.IsNullOrEmpty(orderId))
        {
            var existing = await loyaltyTransactionRepository.GetLatestForOrderSourceAsync(
                orderId, source, cancellationToken);
            if (existing != null)
            {
                return;
            }
        }

        var account = await loyaltyAccountRepository.EnsureForUserAsync(userId, cancellationToken);
        account.GrantPoints(points, source, orderId, actorId);
    }

    public async Task RevokePointsManuallyAsync(
        string userId,
        int points,
        LoyaltyEarnSource source,
        string? orderId,
        string actorId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId) || points <= 0)
        {
            return;
        }

        // Idempotency mirror of GrantPointsManuallyAsync.
        if (!string.IsNullOrEmpty(orderId))
        {
            var existing = await loyaltyTransactionRepository.GetLatestForOrderSourceAsync(
                orderId, source, cancellationToken);
            if (existing != null)
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

        account.RevokePoints(points, source, orderId, actorId);
    }
}
