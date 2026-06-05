using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Memberships;

/// <summary>
/// How often a <see cref="MembershipPlan"/> bills. Mirrors Stripe's
/// recurring.interval shape (we map Monthly → "month", Yearly → "year"
/// when registering the Stripe Price). Stored as int — don't reorder.
/// </summary>
public enum BillingInterval
{
    Monthly = 1,
    Yearly = 2,
}

/// <summary>
/// A purchasable membership plan (e.g. "Cleansia Plus"). Initially zero rows
/// — the entity exists so the pricing/cancellation/matching pipelines can
/// resolve <see cref="UserMembership.MembershipPlan"/> at runtime once the
/// product launches. Adding the actual Plus product is then a single SQL
/// insert + a Stripe Product/Price registration, no code change.
/// </summary>
public class MembershipPlan : Auditable
{
    /// <summary>Stable code referenced from code (e.g. <c>PLUS_MONTHLY</c>). Unique platform-wide.</summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; private set; } = default!;

    /// <summary>Display name shown in subscription management UI.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Display price in CZK for a single billing period. For Monthly plans
    /// this is the per-month price. For Yearly plans this is the full annual
    /// charge — divide by 12 if you need the equivalent per-month figure
    /// (see <see cref="MonthlyEquivalentPriceCzk"/>).
    ///
    /// Canonical price lives in Stripe (referenced via <see cref="StripePriceId"/>);
    /// this mirror lets us preview prices without a Stripe round-trip.
    /// </summary>
    public decimal MonthlyPriceCzk { get; private set; }

    /// <summary>
    /// How often this plan bills. Drives the "save XX%" badge + per-month
    /// equivalent display on the plan switcher.
    /// </summary>
    [Required]
    public BillingInterval BillingInterval { get; private set; } = BillingInterval.Monthly;

    /// <summary>
    /// Optional free trial length in days, applied on the user's first
    /// subscription. 0 = no trial. Stripe handles the trial countdown via
    /// <c>trial_period_days</c> — we just persist the policy here and forward
    /// it on subscription create.
    /// </summary>
    public int TrialPeriodDays { get; private set; }

    /// <summary>
    /// Per-month equivalent of <see cref="MonthlyPriceCzk"/>. For monthly
    /// plans this is the same value; for yearly it's the annual price ÷ 12.
    /// Drives "199 Kč/month, billed annually" copy.
    /// </summary>
    public decimal MonthlyEquivalentPriceCzk => BillingInterval switch
    {
        BillingInterval.Yearly => Math.Round(MonthlyPriceCzk / 12m, 2),
        _ => MonthlyPriceCzk,
    };

    /// <summary>
    /// The Stripe Price id this plan is sold against. One Price per plan.
    /// Required because Stripe subscriptions cannot be created without it.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string StripePriceId { get; private set; } = default!;

    /// <summary>
    /// Discount percentage applied to every cleaning while the membership is
    /// active. e.g. 5.0 = 5% off. Goes through the best-wins pipeline alongside
    /// loyalty tier discount and promo codes — the largest of the three wins,
    /// they do not stack.
    /// </summary>
    public decimal DiscountPercentage { get; private set; }

    /// <summary>
    /// Hours-before-cleaning window in which a member can cancel for free.
    /// Non-members use <c>BookingPolicy.FreeCancellationHours</c>. A Plus
    /// member with this set to 4 can cancel up to 4h before for free; below
    /// that the partial-fee window applies.
    /// </summary>
    public int FreeCancellationWindowHours { get; private set; }

    /// <summary>
    /// Whether this plan grants free express upgrades (skipping the +20%
    /// surcharge for 2-4h lead bookings). When true, usage is capped — see
    /// the future "membership benefit usage" tracker. When false, members
    /// pay the standard surcharge like everyone else.
    /// </summary>
    public bool AllowsExpressUpgrade { get; private set; }

    /// <summary>
    /// Soft-delete flag. Inactive plans aren't offered to new subscribers but
    /// existing <see cref="UserMembership"/> rows referencing them keep working
    /// until they cancel — Stripe is the source of truth for active subscriptions.
    /// </summary>
    [Required]
    public bool IsActive { get; private set; } = true;

    // Private constructor for EF Core
    private MembershipPlan() { }

    /// <summary>
    /// Create a new membership plan. Caller is responsible for creating the
    /// matching Stripe Product + Price first and passing the Price id in.
    /// For yearly plans, <paramref name="monthlyPriceCzk"/> is the full
    /// annual price (the field is named for the dominant monthly case).
    /// </summary>
    public static MembershipPlan Create(
        string code,
        string name,
        decimal monthlyPriceCzk,
        string stripePriceId,
        decimal discountPercentage,
        int freeCancellationWindowHours,
        bool allowsExpressUpgrade,
        BillingInterval billingInterval = BillingInterval.Monthly,
        int trialPeriodDays = 0)
        => new()
        {
            Code = code.ToUpperInvariant(),
            Name = name,
            MonthlyPriceCzk = monthlyPriceCzk,
            StripePriceId = stripePriceId,
            DiscountPercentage = discountPercentage,
            FreeCancellationWindowHours = freeCancellationWindowHours,
            AllowsExpressUpgrade = allowsExpressUpgrade,
            BillingInterval = billingInterval,
            TrialPeriodDays = trialPeriodDays,
        };

    public MembershipPlan UpdatePricing(decimal monthlyPriceCzk, string stripePriceId)
    {
        MonthlyPriceCzk = monthlyPriceCzk;
        StripePriceId = stripePriceId;
        return this;
    }

    public MembershipPlan UpdateBenefits(
        decimal discountPercentage,
        int freeCancellationWindowHours,
        bool allowsExpressUpgrade)
    {
        DiscountPercentage = discountPercentage;
        FreeCancellationWindowHours = freeCancellationWindowHours;
        AllowsExpressUpgrade = allowsExpressUpgrade;
        return this;
    }

    public MembershipPlan Deactivate()
    {
        IsActive = false;
        return this;
    }
}
