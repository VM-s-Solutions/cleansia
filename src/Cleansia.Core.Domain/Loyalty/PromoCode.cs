using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Loyalty;

/// <summary>
/// Admin-issued discount code (e.g. <c>WELCOME20</c>) that customers can redeem
/// at booking time. Tenant-scoped, code stored uppercase, lookup is
/// case-insensitive client-side normalisation.
/// </summary>
public class PromoCode : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(20)]
    public string Code { get; private set; } = default!;

    [Required]
    public PromoCodeType Type { get; private set; }

    /// <summary>0..1 fraction (0.20 = 20%). Set when <see cref="Type"/> is <see cref="PromoCodeType.PercentDiscount"/>.</summary>
    public decimal? DiscountPercent { get; private set; }

    /// <summary>Fixed monetary discount. Set when <see cref="Type"/> is <see cref="PromoCodeType.FixedDiscount"/>.</summary>
    public decimal? DiscountAmount { get; private set; }

    /// <summary>For fixed discounts, the currency the amount is denominated in. Null = tenant default.</summary>
    public string? CurrencyId { get; private set; }
    public Currency? Currency { get; private set; }

    public decimal? MinimumOrderAmount { get; private set; }

    /// <summary>Per-user redemption cap. Default 1 (one-shot codes are the common case).</summary>
    [Required]
    public int MaxRedemptionsPerUser { get; private set; } = 1;

    /// <summary>Optional global cap across all users. Null = unlimited.</summary>
    public int? GlobalMaxRedemptions { get; private set; }

    /// <summary>Denormalised counter bumped on each redemption — keeps the global-cap check O(1).</summary>
    [Required]
    public int CurrentRedemptionsCount { get; private set; }

    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }

    /// <summary>Admin toggle to suspend a code without deleting (preserves audit history).</summary>
    [Required]
    public bool IsActive { get; private set; } = true;

    [MaxLength(500)]
    public string? Description { get; private set; }

    // Private constructor for EF Core
    private PromoCode() { }

    /// <summary>
    /// Create a percent-off code. <paramref name="percent"/> is a 0..1 fraction
    /// (0.20 = 20%). Code is normalised to uppercase.
    /// </summary>
    public static PromoCode CreatePercent(
        string code,
        decimal percent,
        decimal? minimumOrderAmount = null,
        int maxRedemptionsPerUser = 1,
        int? globalMaxRedemptions = null,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required", nameof(code));
        }
        if (percent <= 0m || percent > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be in (0, 1].");
        }

        return new PromoCode
        {
            Code = code.Trim().ToUpperInvariant(),
            Type = PromoCodeType.PercentDiscount,
            DiscountPercent = percent,
            DiscountAmount = null,
            CurrencyId = null,
            MinimumOrderAmount = minimumOrderAmount,
            MaxRedemptionsPerUser = maxRedemptionsPerUser <= 0 ? 1 : maxRedemptionsPerUser,
            GlobalMaxRedemptions = globalMaxRedemptions,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            Description = description,
            IsActive = true,
            CurrentRedemptionsCount = 0,
        };
    }

    /// <summary>
    /// Create a fixed-amount-off code. <paramref name="currencyId"/> identifies
    /// the currency the amount is denominated in. Code is normalised to uppercase.
    /// </summary>
    public static PromoCode CreateFixed(
        string code,
        decimal amount,
        string currencyId,
        decimal? minimumOrderAmount = null,
        int maxRedemptionsPerUser = 1,
        int? globalMaxRedemptions = null,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required", nameof(code));
        }
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }
        if (string.IsNullOrWhiteSpace(currencyId))
        {
            throw new ArgumentException("CurrencyId is required for fixed-discount codes", nameof(currencyId));
        }

        return new PromoCode
        {
            Code = code.Trim().ToUpperInvariant(),
            Type = PromoCodeType.FixedDiscount,
            DiscountPercent = null,
            DiscountAmount = amount,
            CurrencyId = currencyId,
            MinimumOrderAmount = minimumOrderAmount,
            MaxRedemptionsPerUser = maxRedemptionsPerUser <= 0 ? 1 : maxRedemptionsPerUser,
            GlobalMaxRedemptions = globalMaxRedemptions,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            Description = description,
            IsActive = true,
            CurrentRedemptionsCount = 0,
        };
    }

    /// <summary>
    /// Increment the denormalised global redemption counter. Caller is
    /// responsible for inserting the matching <see cref="PromoCodeRedemption"/>
    /// row in the same UnitOfWork transaction.
    /// </summary>
    public void IncrementRedemptions(string actorId)
    {
        CurrentRedemptionsCount += 1;
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    /// <summary>Soft-disable a code without deleting (keeps audit history intact).</summary>
    public void Deactivate(string actorId)
    {
        IsActive = false;
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Admin edit of the mutable fields. <see cref="Code"/>, <see cref="Type"/>,
    /// <see cref="DiscountPercent"/>, <see cref="DiscountAmount"/> and
    /// <see cref="CurrencyId"/> are deliberately immutable — admins must
    /// deactivate and re-issue a code to change those (avoids audit-history
    /// rewriting on already-redeemed codes).
    /// </summary>
    public void Update(
        bool isActive,
        DateTimeOffset? validFrom,
        DateTimeOffset? validUntil,
        decimal? minimumOrderAmount,
        int maxRedemptionsPerUser,
        int? globalMaxRedemptions,
        string? description,
        string actorId)
    {
        IsActive = isActive;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        MinimumOrderAmount = minimumOrderAmount;
        MaxRedemptionsPerUser = maxRedemptionsPerUser <= 0 ? 1 : maxRedemptionsPerUser;
        GlobalMaxRedemptions = globalMaxRedemptions;
        Description = description;
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// True if the code is structurally redeemable at <paramref name="now"/> —
    /// active, within the validity window, and below the global cap. Per-user
    /// cap and minimum-order checks happen in the service layer (need extra
    /// state).
    /// </summary>
    public bool IsRedeemableAt(DateTimeOffset now) =>
        IsActive
        && (ValidFrom == null || now >= ValidFrom)
        && (ValidUntil == null || now <= ValidUntil)
        && (GlobalMaxRedemptions == null || CurrentRedemptionsCount < GlobalMaxRedemptions);
}
