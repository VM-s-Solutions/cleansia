using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Loyalty;

/// <summary>
/// Aggregate root for the loyalty subsystem — 1:1 with <see cref="User"/>.
/// Holds denormalized progress fields (<see cref="LifetimePoints"/>,
/// <see cref="CurrentTier"/>, <see cref="CompletedBookingsCount"/>) which are
/// recomputed on each grant/revoke from the append-only ledger of
/// <see cref="LoyaltyTransaction"/> entries.
/// </summary>
public class LoyaltyAccount : Auditable, ITenantEntity
{
    [Required]
    public string UserId { get; private set; } = default!;
    public User? User { get; private set; }

    [Required]
    public int LifetimePoints { get; private set; }

    [Required]
    public LoyaltyTier CurrentTier { get; private set; }

    [Required]
    public DateTimeOffset TierAchievedOn { get; private set; }

    [Required]
    public int CompletedBookingsCount { get; private set; }

    private readonly List<LoyaltyTransaction> _transactions = new();
    public IReadOnlyCollection<LoyaltyTransaction> Transactions => _transactions.AsReadOnly();

    // Private constructor for EF Core
    private LoyaltyAccount() { }

    public static LoyaltyAccount Create(string userId)
    {
        return new LoyaltyAccount
        {
            UserId = userId,
            LifetimePoints = 0,
            CurrentTier = LoyaltyTier.BronzeCleaner,
            TierAchievedOn = DateTimeOffset.UtcNow,
            CompletedBookingsCount = 0,
        };
    }

    /// <summary>
    /// Append an Earn ledger entry and recompute denormalized fields.
    /// Caller passes the positive points value; the ledger row stores it as-is.
    /// <paramref name="idempotencyKey"/> (T-0112 / S7a) is the client-supplied
    /// token for the manual admin path; null on the order-driven / referral
    /// paths. It is persisted on the ledger row and backed by a filtered unique
    /// index so a double-submit of the same admin grant collapses onto one row.
    /// </summary>
    public void GrantPoints(int points, LoyaltyEarnSource source, string? orderId, string actorId, string? idempotencyKey = null)
    {
        if (points <= 0)
        {
            return;
        }

        var tx = LoyaltyTransaction.Create(Id, LoyaltyTransactionType.Earn, points, source, orderId, idempotencyKey: idempotencyKey);
        _transactions.Add(tx);

        LifetimePoints += points;
        if (source == LoyaltyEarnSource.OrderCompleted)
        {
            CompletedBookingsCount += 1;
        }

        RecomputeTier();
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Append a Revoke ledger entry (stored as negative points) and recompute
    /// denormalized fields. Caller passes the positive magnitude to revoke.
    /// <paramref name="idempotencyKey"/> (T-0112 / S7a) is the client-supplied
    /// token for the manual admin path; null on the order-driven path. Backed
    /// by the same filtered unique index so a double-submit collapses to one row.
    /// </summary>
    public void RevokePoints(int points, LoyaltyEarnSource source, string? orderId, string actorId, string? idempotencyKey = null)
    {
        if (points <= 0)
        {
            return;
        }

        var tx = LoyaltyTransaction.Create(Id, LoyaltyTransactionType.Revoke, -points, source, orderId, idempotencyKey: idempotencyKey);
        _transactions.Add(tx);

        LifetimePoints = Math.Max(0, LifetimePoints - points);
        if (source == LoyaltyEarnSource.OrderCompleted)
        {
            CompletedBookingsCount = Math.Max(0, CompletedBookingsCount - 1);
        }

        RecomputeTier();
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    private void RecomputeTier()
    {
        var newTier = LifetimePoints switch
        {
            >= 5000 => LoyaltyTier.PlatinumSparkler,
            >= 2000 => LoyaltyTier.GoldPolisher,
            >= 500 => LoyaltyTier.SilverMopper,
            _ => LoyaltyTier.BronzeCleaner,
        };

        if (newTier != CurrentTier)
        {
            CurrentTier = newTier;
            TierAchievedOn = DateTimeOffset.UtcNow;
        }
    }
}
