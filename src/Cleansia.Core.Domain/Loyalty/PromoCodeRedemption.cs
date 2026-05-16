using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Loyalty;

/// <summary>
/// Append-only audit row recording a single use of a <see cref="PromoCode"/>
/// against a specific <see cref="Order"/> by a specific <see cref="User"/>.
/// One row per (order, code) — see <c>OrderId</c> unique index.
/// </summary>
public class PromoCodeRedemption : Auditable, ITenantEntity
{
    [Required]
    public string PromoCodeId { get; private set; } = default!;
    public PromoCode? PromoCode { get; private set; }

    [Required]
    public string UserId { get; private set; } = default!;
    public User? User { get; private set; }

    [Required]
    public string OrderId { get; private set; } = default!;
    public Order? Order { get; private set; }

    [Required]
    public decimal AppliedDiscount { get; private set; }

    [Required]
    public DateTimeOffset RedeemedOn { get; private set; }

    // Private constructor for EF Core
    private PromoCodeRedemption() { }

    public static PromoCodeRedemption Create(
        string promoCodeId,
        string userId,
        string orderId,
        decimal appliedDiscount)
    {
        if (string.IsNullOrWhiteSpace(promoCodeId))
        {
            throw new ArgumentException("PromoCodeId is required", nameof(promoCodeId));
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required", nameof(userId));
        }
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("OrderId is required", nameof(orderId));
        }
        if (appliedDiscount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(appliedDiscount), "Applied discount cannot be negative.");
        }

        return new PromoCodeRedemption
        {
            PromoCodeId = promoCodeId,
            UserId = userId,
            OrderId = orderId,
            AppliedDiscount = appliedDiscount,
            RedeemedOn = DateTimeOffset.UtcNow,
        };
    }
}
