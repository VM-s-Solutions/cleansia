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
    ICompanyInfoRepository companyInfoRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    IVatCalculator vatCalculator,
    ILoyaltyService loyaltyService,
    IUserMembershipRepository userMembershipRepository) : IOrderFactory
{
    /// <summary>
    /// LOY-003 — Hard cap on combined (Plus + tier) discount, applied as a
    /// fraction of raw subtotal. Promo can exceed this cap because it's a
    /// per-campaign decision, but the additive tier+Plus combination never
    /// does. Keep in lock-step with <see cref="QuoteOrder"/> and the customer
    /// wizard summary so the displayed price matches what gets persisted.
    /// </summary>
    public const decimal MaxCombinedDiscountFraction = 0.12m;

    public async Task<Order> CreateAsync(CreateOrderInput input, CancellationToken cancellationToken)
    {
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

        decimal? appliedTierDiscount = resolution.TierAmount > 0m ? resolution.TierAmount : null;
        decimal? appliedMembershipDiscount = resolution.MembershipAmount > 0m ? resolution.MembershipAmount : null;
        decimal? appliedPromoDiscount = resolution.PromoAmount > 0m ? resolution.PromoAmount : null;
        string? appliedPromoCodeId = resolution.PromoAmount > 0m ? input.PromoCodeId : null;
        string? appliedMembershipPlanId = resolution.MembershipAmount > 0m ? membershipPlanId : null;
        LoyaltyTier? appliedTierAtPurchase = resolution.TierAmount > 0m ? tierAtPurchase : null;
        var appliedAmount = resolution.TotalAmount;

        // Discount on raw subtotal first, then express surcharge on top of
        // the discounted price. Same order as CreateOrder.Handler used
        // post-LOY-FOLLOWUP-1 — keep these branches in lock-step.
        var nowUtc = DateTime.UtcNow;
        var discountedSubtotal = input.RawSubtotal - appliedAmount;
        var finalTotalPrice = discountedSubtotal;
        if (BookingPolicy.RequiresExpressSurcharge(input.CleaningDate, nowUtc))
        {
            finalTotalPrice = discountedSubtotal * (1 + BookingPolicy.ExpressSurchargeRate);
        }

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
            recurringTemplateId: input.RecurringTemplateId);

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

        var estimatedTime = selectedServices.Sum(s => s.Service!.EstimatedTime) +
                            selectedPackages.Sum(p => p.Package!.IncludedServices.Sum(s => s.Service!.EstimatedTime));
        order.UpdateEstimatedTime(estimatedTime);
        order.CalculateRequiredEmployees();

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
        orderRepository.Add(order);
        return order;
    }

    /// <summary>
    /// LOY-003 — additive Plus + tier with 12% cap on the combined amount.
    /// Promo replaces the combined if larger. Tier floor enforcement is
    /// upstream (LoyaltyService); by the time amounts reach here, a below-
    /// floor tier discount is already 0.
    ///
    /// Returns the per-source amounts that actually applied so the caller
    /// can persist + render them. When the combined Plus+Tier amount would
    /// exceed the cap, both amounts are pro-rated down so their sum equals
    /// the cap; this keeps each source's share visible on the receipt
    /// rather than zeroing one out. Promo, when it wins, fully replaces
    /// the combined (both Plus and Tier go to 0 in the output).
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
    /// Per-source amounts after LOY-003 cap + promo-replacement resolution.
    /// Either (Membership + Tier) or Promo is non-zero, never both — the
    /// promo branch zeroes the additive pair. Both Membership and Tier
    /// can be non-zero simultaneously in the combined branch.
    /// </summary>
    internal record DiscountResolution(
        decimal MembershipAmount,
        decimal TierAmount,
        decimal PromoAmount,
        decimal TotalAmount);
}
