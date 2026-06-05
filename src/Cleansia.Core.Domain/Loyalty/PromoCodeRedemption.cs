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

    /// <summary>
    /// 0-based per-user redemption slot, in <c>[0, PromoCode.MaxRedemptionsPerUser - 1]</c>. Together
    /// with <c>(TenantId, PromoCodeId, UserId)</c> it forms the tenant-scoped unique index
    /// (T-0110 / S8) that hard-caps per-user redemptions: a one-shot code (default
    /// <c>MaxRedemptionsPerUser = 1</c>) only ever has slot <c>0</c>, while an <c>M &gt; 1</c> code
    /// keeps slots <c>0..M-1</c> valid. The ordinal is DERIVED from the atomic slot reservation
    /// (<see cref="Cleansia.Core.Domain.Repositories.IPromoCodeRedemptionRepository.TryReserveRedemptionSlotAsync"/>),
    /// never from a pre-read count — a pre-read ordinal would let two concurrent <c>M &gt; 1</c>
    /// redemptions collide on the same ordinal and falsely reject the loser.
    /// </summary>
    [Required]
    public int SlotOrdinal { get; private set; }

    // Private constructor for EF Core
    private PromoCodeRedemption() { }

    /// <summary>
    /// Build a redemption row whose <see cref="SlotOrdinal"/> was assigned by the atomic
    /// per-user slot reservation (T-0110). This is the only entry point now that the per-user cap
    /// is DB-enforced on the slot — the legacy parameterless-ordinal <c>Create</c> is gone so no
    /// caller can mint a row without a reserved ordinal.
    /// </summary>
    public static PromoCodeRedemption CreateReserved(
        string promoCodeId,
        string userId,
        string orderId,
        decimal appliedDiscount,
        int slotOrdinal)
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
        if (slotOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotOrdinal), "Slot ordinal cannot be negative.");
        }

        return new PromoCodeRedemption
        {
            PromoCodeId = promoCodeId,
            UserId = userId,
            OrderId = orderId,
            AppliedDiscount = appliedDiscount,
            SlotOrdinal = slotOrdinal,
            RedeemedOn = DateTimeOffset.UtcNow,
        };
    }
}
