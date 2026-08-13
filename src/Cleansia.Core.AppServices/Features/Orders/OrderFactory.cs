using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Order = Cleansia.Core.Domain.Orders.Order;
using OrderService = Cleansia.Core.Domain.Orders.OrderService;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Default <see cref="IOrderFactory"/>. Builds + persists a fully-formed
/// <see cref="Order"/> with discount snapshot, services + packages, VAT
/// breakdown, and an initial <see cref="OrderStatus.New"/> status track.
///
/// Keep the discount math here (best-of-three between tier, membership, and
/// promo) in lock-step with the wizard summary + QuoteOrder so the displayed
/// price always matches what the user pays. See
/// <c>BookingPolicy.RequiresExpressSurcharge</c> for the surcharge rule.
/// </summary>
public sealed class OrderFactory(
    IOrderRepository orderRepository,
    IServiceRepository serviceRepository,
    IPackageRepository packageRepository,
    IEmployeePayConfigRepository payConfigRepository,
    ICompanyInfoRepository companyInfoRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    IVatCalculator vatCalculator,
    ILoyaltyService loyaltyService,
    IUserMembershipRepository userMembershipRepository,
    IPreferredCleanerHoldResolver preferredCleanerHoldResolver,
    INotificationProducer notificationProducer) : IOrderFactory
{
    /// <summary>
    /// Hard cap on the combined (Plus + tier) discount, as a fraction of raw subtotal. <b>12% is an
    /// OWNER RULING, not a tuning value</b> — raising it is a product decision, not a bug fix, even
    /// though it reads like one. <b>The consequence is stated in member-facing copy on web, Android and
    /// iOS in five locales each: change this number and that copy becomes false.</b> Keep in lock-step
    /// with <see cref="QuoteOrder"/> and the wizard summary. → /product/business-rules#discount-cap
    /// </summary>
    public const decimal MaxCombinedDiscountFraction = 0.12m;

    public async Task<Order> CreateAsync(CreateOrderInput input, CancellationToken cancellationToken)
    {
        // An order whose selection has no platform-wide pay config quotes NOTHING on every cleaner's
        // board at once and counts as zero in their earnings. The gate sits here rather than only in
        // CreateOrder.Validator because the recurring materializer reaches this factory without running
        // that validator — the same reason the booked-span cap is enforced in both places. First,
        // before any pricing work, so the refusal costs one query.
        var payCoverageGaps = await PayCoverageLookup.FindSelectionGapsAsync(
            serviceRepository, packageRepository, payConfigRepository,
            input.SelectedServiceIds, input.SelectedPackageIds, cancellationToken);

        if (payCoverageGaps.Count > 0)
        {
            throw new InvalidOperationException(
                "No platform-wide EmployeePayConfig covers: "
                + string.Join(", ", payCoverageGaps.Select(gap => $"{gap.Kind} '{gap.Name}'"))
                + ". An order carrying it would show no pay to any cleaner.");
        }

        // Resolve the tier discount + membership discount given the user.
        // Anonymous (guest) bookings skip both and only see promo if the
        // caller already validated one. Snapshot stays on the Order so
        // receipts stay accurate even if the user's tier later changes.
        //
        // Tier discount respects the per-tier floor (today 1000 CZK uniformly,
        // enforced in LoyaltyService). Plus discount has no floor — paying
        // subscribers always see value, even on small orders.
        decimal tierDiscount = 0m;
        LoyaltyTier? tierAtPurchase = null;
        decimal membershipDiscount = 0m;
        string? membershipPlanId = null;

        if (!string.IsNullOrEmpty(input.UserId))
        {
            var tierResult = await loyaltyService.ResolveTierDiscountForOrderAsync(
                input.UserId, input.RawSubtotal, cancellationToken);
            tierAtPurchase = tierResult.TierAtPurchase;
            tierDiscount = tierResult.DiscountAmount > 0m ? tierResult.DiscountAmount : 0m;

            var activeMembership = await userMembershipRepository
                .GetActiveForUserAsync(input.UserId, cancellationToken);
            if (activeMembership != null)
            {
                membershipDiscount = input.RawSubtotal
                    * (activeMembership.MembershipPlan.DiscountPercentage / 100m);
                membershipPlanId = activeMembership.MembershipPlanId;
            }
        }

        // LOY-003 — additive Plus + tier capped at 12%. Promo replaces the
        // combined value if the promo is larger. See AppliedDiscountSource
        // for the resulting enum semantics.
        var resolution = ResolveLoy003Discount(
            membershipDiscount, tierDiscount, input.PromoDiscountAmount, input.RawSubtotal);

        var surchargeApplies = BookingPolicy.RequiresExpressSurcharge(
            input.CleaningDate,
            input.NowUtc,
            waiverApplies: input.ReservedExpressWaiver != null);

        var finalTotalPrice = BookingPolicy.ApplyExpressSurcharge(
            input.RawSubtotal - resolution.TotalAmount, surchargeApplies);

        var applied = resolution.AsChargedAgainst(surchargeApplies);

        decimal? appliedTierDiscount = applied.TierAmount > 0m ? applied.TierAmount : null;
        decimal? appliedMembershipDiscount = applied.MembershipAmount > 0m ? applied.MembershipAmount : null;
        decimal? appliedPromoDiscount = applied.PromoAmount > 0m ? applied.PromoAmount : null;
        string? appliedPromoCodeId = applied.PromoAmount > 0m ? input.PromoCodeId : null;
        string? appliedMembershipPlanId = applied.MembershipAmount > 0m ? membershipPlanId : null;
        LoyaltyTier? appliedTierAtPurchase = applied.TierAmount > 0m ? tierAtPurchase : null;

        var order = Order.Create(
            input.CustomerName,
            input.CustomerEmail,
            input.CustomerPhone,
            input.Address,
            input.Rooms,
            input.Bathrooms,
            input.Extras,
            input.CleaningDate,
            input.PaymentType,
            finalTotalPrice,
            input.Currency.Id,
            PaymentStatus.Pending,
            userId: input.UserId,
            tierDiscountAmount: appliedTierDiscount,
            tierAtPurchase: appliedTierAtPurchase,
            promoDiscountAmount: appliedPromoDiscount,
            promoCodeId: appliedPromoCodeId,
            membershipDiscountAmount: appliedMembershipDiscount,
            membershipPlanIdAtPurchase: appliedMembershipPlanId,
            preferredEmployeeId: input.PreferredEmployeeId,
            recurringTemplateId: input.RecurringTemplateId,
            specialInstructions: input.SpecialInstructions,
            accessInstructions: input.AccessInstructions);

        order.SetCurrency(input.Currency);

        var selectedServices = await serviceRepository
            .GetByIds(input.SelectedServiceIds)
            .Select(s => OrderService.Create(order, s))
            .ToListAsync(cancellationToken);
        var selectedPackages = await packageRepository
            .GetByIds(input.SelectedPackageIds)
            .Include(p => p.IncludedServices)
                .ThenInclude(s => s.Service)
            .Select(p => OrderPackage.Create(order, p))
            .ToListAsync(cancellationToken);

        order.AddSelectedServices(selectedServices);
        order.AddSelectedPackages(selectedPackages);

        var estimatedTime = OrderDuration.EstimateMinutes(
            selectedServices.Select(s => s.Service!),
            selectedPackages.Select(p => p.Package!));

        // Ahead of CalculateRequiredEmployees, so an over-cap span cannot mint a crew on its way out.
        // CreateOrder.Validator turns this into a business error for the customer; this is the backstop
        // for callers that never run it (the recurring materializer).
        if (BookingPolicy.ExceedsMaxBookableSpan(estimatedTime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                estimatedTime,
                $"Booked estimate exceeds BookingPolicy.MaxBookableOrderSpanHours "
                + $"({BookingPolicy.MaxBookableOrderSpanMinutes} min).");
        }

        order.UpdateEstimatedTime(estimatedTime);
        order.CalculateRequiredEmployees(BookingPolicy.SpareSeatsPerOrder);

        // The factory never assigns either hold column itself — it hands the resolver's answer to the
        // aggregate, which owns the (beneficiary, deadline) pair. A declined hold is not a failure:
        // the preference is still stored, and the order goes straight to the open board.
        var preferredCleaner = await preferredCleanerHoldResolver.ResolveAsync(
            input.UserId,
            input.PreferredEmployeeId,
            input.Address.CountryId,
            input.CleaningDate,
            estimatedTime,
            input.NowUtc,
            cancellationToken);

        if (preferredCleaner.HoldUntilUtc is { } holdUntilUtc)
        {
            order.GrantPreferredHold(
                input.PreferredEmployeeId!,
                holdUntilUtc,
                input.NowUtc,
                BookingPolicy.MaxPreferredOfferRounds);
        }

        // VAT breakdown — gracefully degrade when there's no company info
        // configured for the country (sets net = total, vat = 0).
        var countryId = input.Address.CountryId;
        var companyInfo = await companyInfoRepository.GetActiveByCountryAsync(countryId, cancellationToken)
                          ?? await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);
        if (companyInfo != null)
        {
            var countryConfig = await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
            var vatBreakdown = vatCalculator.Calculate(order.TotalPrice, companyInfo, countryConfig);
            order.SetVatBreakdown(vatBreakdown.NetAmount, vatBreakdown.VatAmount, vatBreakdown.AppliedRate);
        }
        else
        {
            order.SetVatBreakdown(order.TotalPrice, 0m, null);
        }

        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));

        // Sits after the only write of CurrentStatus because the seam reads that column; before the
        // write it reads New only because New happens to be the enum's zero.
        //
        // The notify predicate is WIDER than the hold's (ADR-0036 D4.1) — too little lead time to
        // withhold a seat still earns the targeted offer — and NARROWER than the preference: an order
        // that is not yet offerable is announced by whichever site makes it so (Q-BROWSE-01 (b)), the
        // Stripe webhook for card and the customer's confirm for recurring. A cash one-off is offerable
        // the moment it exists, which is why it is the only shape announced here.
        await PreferredOfferNotifier.NotifyIfOfferableAsync(
            order, preferredCleaner.Recipient, notificationProducer, cancellationToken);

        orderRepository.Add(order);
        return order;
    }

    /// <summary>
    /// Additive Plus + tier, capped; promo replaces the combined if larger. Over the cap, both are
    /// PRO-RATED rather than one zeroed, so each source stays visible on the receipt. Tier-floor
    /// enforcement is upstream — a below-floor tier discount is already 0 by the time it arrives.
    /// → /product/business-rules#discount-cap
    /// </summary>
    internal static DiscountResolution ResolveLoy003Discount(
        decimal membershipDiscount,
        decimal tierDiscount,
        decimal promoDiscount,
        decimal rawSubtotal)
    {
        // Apply the 12% combined cap on (Plus + tier). Each source keeps a
        // proportional share when capping kicks in, so the user can see both
        // chips on the receipt with the right amounts.
        var combinedRaw = membershipDiscount + tierDiscount;
        var combinedCap = rawSubtotal * MaxCombinedDiscountFraction;
        decimal cappedMembership = membershipDiscount;
        decimal cappedTier = tierDiscount;
        if (combinedRaw > combinedCap && combinedRaw > 0m)
        {
            var scale = combinedCap / combinedRaw;
            cappedMembership = Math.Round(membershipDiscount * scale, 2, MidpointRounding.AwayFromZero);
            cappedTier = Math.Round(tierDiscount * scale, 2, MidpointRounding.AwayFromZero);
        }
        var combined = cappedMembership + cappedTier;

        // Promo replaces the combined if it's larger. No stacking — keeps
        // promo campaigns predictable and prevents stacking a code on top
        // of a Plus+Gold combo that's already at the cap.
        if (promoDiscount > combined && promoDiscount > 0m)
        {
            return new DiscountResolution(
                MembershipAmount: 0m,
                TierAmount: 0m,
                PromoAmount: promoDiscount,
                TotalAmount: promoDiscount);
        }

        return new DiscountResolution(
            MembershipAmount: cappedMembership,
            TierAmount: cappedTier,
            PromoAmount: 0m,
            TotalAmount: combined);
    }

    /// <summary>
    /// Per-source amounts after LOY-003 cap + promo-replacement resolution, measured against the RAW
    /// pre-surcharge subtotal. Either (Membership + Tier) or Promo is non-zero, never both — the
    /// promo branch zeroes the additive pair. Both Membership and Tier
    /// can be non-zero simultaneously in the combined branch.
    /// </summary>
    internal record DiscountResolution(
        decimal MembershipAmount,
        decimal TierAmount,
        decimal PromoAmount,
        decimal TotalAmount)
    {
        /// <summary>
        /// The resolution restated against the price actually charged — <b>the ONE form that may be
        /// reported, persisted or rendered.</b> Resolution happens on the raw pre-surcharge subtotal, but
        /// the price it comes off carries the surcharge, so the raw figure under-states the saving. <b>The
        /// order carries no express flag for any consumer to correct with, so the correction can only be
        /// made HERE, before the amount is written.</b>
        /// → /product/business-rules#discount-express-correction
        /// </summary>
        internal DiscountResolution AsChargedAgainst(bool surchargeApplies)
        {
            decimal Charged(decimal amount) => BookingPolicy.ApplyExpressSurcharge(amount, surchargeApplies);

            return new DiscountResolution(
                Charged(MembershipAmount), Charged(TierAmount), Charged(PromoAmount), Charged(TotalAmount));
        }
    }
}
