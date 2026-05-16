using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Loyalty;

/// <summary>
/// Append-only ledger entry recording an earn or revoke event against a
/// <see cref="LoyaltyAccount"/>. <see cref="Points"/> is signed
/// (positive on Earn, negative on Revoke).
/// </summary>
public class LoyaltyTransaction : Auditable, ITenantEntity
{
    [Required]
    public string LoyaltyAccountId { get; private set; } = default!;
    public LoyaltyAccount? Account { get; private set; }

    [Required]
    public LoyaltyTransactionType Type { get; private set; }

    /// <summary>
    /// Signed points delta. Positive on <see cref="LoyaltyTransactionType.Earn"/>,
    /// negative on <see cref="LoyaltyTransactionType.Revoke"/>.
    /// </summary>
    [Required]
    public int Points { get; private set; }

    [Required]
    public LoyaltyEarnSource Source { get; private set; }

    [MaxLength(50)]
    public string? OrderId { get; private set; }

    [MaxLength(500)]
    public string? Description { get; private set; }

    [Required]
    public DateTimeOffset OccurredOn { get; private set; }

    // Private constructor for EF Core
    private LoyaltyTransaction() { }

    public static LoyaltyTransaction Create(
        string accountId,
        LoyaltyTransactionType type,
        int signedPoints,
        LoyaltyEarnSource source,
        string? orderId,
        string? description = null)
    {
        return new LoyaltyTransaction
        {
            LoyaltyAccountId = accountId,
            Type = type,
            Points = signedPoints,
            Source = source,
            OrderId = orderId,
            Description = description,
            OccurredOn = DateTimeOffset.UtcNow,
        };
    }
}
