using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Memberships;

/// <summary>
/// How often a <see cref="MembershipPlan"/> bills. Mirrors Stripe's
/// recurring.interval shape (we map Monthly → "month", Yearly → "year"
/// when registering the Stripe Price). Stored as int — don't reorder.
/// </summary>
[SwaggerEnumAsInt]
public enum BillingInterval
{
    Monthly = 1,
    Yearly = 2,
}

/// <summary>
/// A purchasable membership plan (e.g. "Cleansia Plus"): the benefits and the billing cadence. What it
/// costs, and which Stripe Price charges it, lives on <see cref="MembershipPlanPrice"/> — one row per
/// currency, so the same plan is sold in every market at that market's own figure.
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
    /// The per-month figure a price of this plan renders as: the price itself for a monthly plan, the
    /// annual charge ÷ 12 for a yearly one. Lives here rather than on the price row because the cadence
    /// is the plan's; the row only supplies the number.
    /// </summary>
    public decimal MonthlyEquivalentOf(decimal price) => BillingInterval switch
    {
        BillingInterval.Yearly => Math.Round(price / 12m, 2),
        _ => price,
    };

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
    /// ADR-0035 D2.1 — how many express waivers this plan grants per calendar month. Mirrors
    /// <see cref="FreeCancellationWindowHours"/> in role: the benefit's number belongs on the plan, next
    /// to the benefit it meters, where an admin can change it without a deploy and a second tier can
    /// differ. <c>0</c> means no waiver (fail-closed), the same semantic the cancellation window uses;
    /// "unlimited" is deliberately not expressible, so a seeding mistake cannot become an unbounded perk.
    /// Ignored entirely when <see cref="AllowsExpressUpgrade"/> is false.
    /// </summary>
    public int ExpressUpgradesPerMonth { get; private set; }

    /// <summary>
    /// Soft-delete flag. Inactive plans aren't offered to new subscribers but
    /// existing <see cref="UserMembership"/> rows referencing them keep working
    /// until they cancel — Stripe is the source of truth for active subscriptions.
    /// </summary>
    [Required]
    public bool IsActive { get; private set; } = true;

    // Private constructor for EF Core
    private MembershipPlan() { }

    public static MembershipPlan Create(
        string code,
        string name,
        decimal discountPercentage,
        int freeCancellationWindowHours,
        bool allowsExpressUpgrade,
        BillingInterval billingInterval = BillingInterval.Monthly,
        int trialPeriodDays = 0,
        int expressUpgradesPerMonth = 0)
        => new()
        {
            Code = code.ToUpperInvariant(),
            Name = name,
            DiscountPercentage = discountPercentage,
            FreeCancellationWindowHours = freeCancellationWindowHours,
            AllowsExpressUpgrade = allowsExpressUpgrade,
            BillingInterval = billingInterval,
            TrialPeriodDays = trialPeriodDays,
            ExpressUpgradesPerMonth = expressUpgradesPerMonth,
        };

    public MembershipPlan UpdateName(string name)
    {
        Name = name;
        return this;
    }

    public MembershipPlan UpdateTrial(int trialPeriodDays)
    {
        TrialPeriodDays = trialPeriodDays;
        return this;
    }

    public MembershipPlan UpdateBenefits(
        decimal discountPercentage,
        int freeCancellationWindowHours,
        bool allowsExpressUpgrade,
        int expressUpgradesPerMonth)
    {
        DiscountPercentage = discountPercentage;
        FreeCancellationWindowHours = freeCancellationWindowHours;
        AllowsExpressUpgrade = allowsExpressUpgrade;
        ExpressUpgradesPerMonth = expressUpgradesPerMonth;
        return this;
    }

    public MembershipPlan Deactivate()
    {
        IsActive = false;
        return this;
    }
}
