using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class QuoteOrder
{
    public record Command(
        IEnumerable<string> SelectedServiceIds,
        IEnumerable<string> SelectedPackageIds,
        int Rooms,
        int Bathrooms,
        string? CurrencyId,
        // Extras slugs (e.g. "inside-oven") — empty when the wizard hasn't
        // surfaced extras yet, e.g. on the first step before "Confirm".
        IEnumerable<string>? SelectedExtraSlugs = null,
        // Optional — when the user has picked a slot the surcharge is
        // included in the returned totals. Null skips the surcharge check
        // (initial wizard quote, no slot yet).
        DateTime? CleaningDate = null) : ICommand<Response>;

    /// <summary>
    /// Quote response. <see cref="TotalPrice"/> is the undiscounted total INCLUDING any express
    /// surcharge (exactly <c>IOrderPricingCalculator.CalculateAsync</c>'s TotalPrice) and is what
    /// <c>CreateOrder.PriceMatchesAsync</c> validates against — clients must
    /// submit this value unchanged. <see cref="FinalPriceAfterDiscount"/> is the
    /// display price after the best-of-three (tier vs membership) discount, computed the way
    /// <c>OrderFactory</c> persists it: discount off the pre-surcharge subtotal, surcharge on top.
    /// <see cref="OriginalSubtotal"/> is that price plus the discount, so it always equals the
    /// <c>OrderItem.OriginalSubtotal</c> the order detail page will show for the same booking — and,
    /// because the discount is reported against the charged price, equals <see cref="TotalPrice"/>.
    /// Both are the undiscounted price; two fields that disagreed were the express-composition defect.
    /// Promo isn't included here (entered at checkout, applied at create-time).
    ///
    /// <see cref="ExtrasSubtotal"/>, <see cref="ExpressSurchargeApplied"/>,
    /// <see cref="ExpressSurchargeAmount"/> let the wizard render a
    /// transparent line-item breakdown (extras row + surcharge row).
    /// </summary>
    public record Response(
        decimal TotalPrice,
        decimal FinalPriceAfterDiscount,
        decimal OriginalSubtotal,
        AppliedDiscountSource AppliedDiscountSource,
        decimal? TierDiscountAmount,
        decimal? MembershipDiscountAmount,
        decimal? TierDiscountMinOrderAmount,
        string CurrencyId,
        string CurrencyCode,
        decimal ServicesSubtotal,
        decimal PackagesSubtotal,
        decimal ExtrasSubtotal,
        bool ExpressSurchargeApplied,
        decimal ExpressSurchargeAmount,
        decimal ExchangeRate,
        /// <summary>
        /// The slot IS express and the surcharge was nevertheless not charged, because the member has a
        /// free express upgrade left. Without this field <c>ExpressSurchargeApplied: false</c> is
        /// ambiguous between "waived" and "not an express slot at all", and the wizard cannot show the
        /// waiver in place of the surcharge.
        /// </summary>
        bool ExpressSurchargeWaivedByMembership = false,
        /// <summary>
        /// Free express upgrades left this calendar month BEFORE this booking — server-computed, never
        /// client-counted (a client that counts its own orders disagrees with the server the first time a
        /// cancellation releases a slot). Null when the caller has no membership.
        /// </summary>
        int? ExpressUpgradesRemaining = null);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IServiceRepository _serviceRepository;
        private readonly IPackageRepository _packageRepository;

        public Validator(
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository,
            ICurrencyRepository currencyRepository)
        {
            _serviceRepository = serviceRepository;
            _packageRepository = packageRepository;

            RuleFor(x => x.Rooms)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.Bathrooms)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.SelectedServiceIds)
                .MustAsync(serviceRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices);

            RuleFor(x => x.SelectedPackageIds)
                .MustAsync(packageRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage);

            When(x => !string.IsNullOrEmpty(x.CurrencyId), () =>
            {
                RuleFor(x => x.CurrencyId!)
                    .MustAsync(currencyRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.InvalidCurrency);
            });

            RuleFor(x => x)
                .MustAsync(SpanWithinCapAsync)
                .WithMessage(BusinessErrorMessage.OrderSpanExceedsMaximum);
        }

        /// <summary>
        /// The same bound <c>CreateOrder.Validator</c> draws, drawn one step earlier: without it a
        /// selection the platform will refuse to book comes back priced, and the customer only learns
        /// at submit. Same predicate, same key, same arithmetic — an empty selection sums to 0 and
        /// still quotes, because the wizard quotes before anything is picked.
        /// </summary>
        private async Task<bool> SpanWithinCapAsync(Command command, CancellationToken cancellationToken)
        {
            var serviceMinutes = await _serviceRepository
                .GetByIds(command.SelectedServiceIds)
                .SumAsync(s => s.EstimatedTime, cancellationToken);

            var packagedServiceMinutes = await _packageRepository
                .GetByIds(command.SelectedPackageIds)
                .SelectMany(p => p.IncludedServices)
                .SumAsync(ps => ps.Service!.EstimatedTime, cancellationToken);

            return !BookingPolicy.ExceedsMaxBookableSpan(serviceMinutes + packagedServiceMinutes);
        }
    }

    public class Handler(
        IOrderPricingCalculator pricingCalculator,
        IUserSessionProvider userSessionProvider,
        ILoyaltyService loyaltyService,
        ILoyaltyTierConfigRepository loyaltyTierConfigRepository,
        IUserMembershipRepository userMembershipRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var result = await pricingCalculator.CalculateAsync(
                command.SelectedServiceIds,
                command.SelectedPackageIds,
                command.SelectedExtraSlugs ?? Array.Empty<string>(),
                command.Rooms,
                command.Bathrooms,
                command.CurrencyId,
                command.CleaningDate,
                userSessionProvider.GetUserId(),
                nowUtc,
                cancellationToken);

            // Two different bases, deliberately. The gross (surcharge included) is what the client
            // must resubmit — CreateOrder.PriceMatchesAsync compares it against the same calculator
            // call. The discount is resolved on the RAW pre-surcharge subtotal because that is what
            // OrderFactory persists; discounting the gross yields the same final charge but a bigger
            // itemised saving than the receipt (and the lifetime-savings stat) will ever show.
            var grossSubtotal = result.TotalPrice;
            var rawSubtotal = grossSubtotal - result.ExpressSurchargeAmount;
            var userId = userSessionProvider.GetUserId();

            // Anonymous quote (guest checkout) — no discount preview possible.
            // Promo discount is intentionally excluded from the quote: the
            // promo code is entered at the checkout step, not at the quote
            // step, so we can't preview it here. CreateOrder applies the
            // best-of-three including promo at submit time.
            decimal tierDiscount = 0m;
            decimal? tierMinOrderAmount = null;
            decimal membershipDiscount = 0m;

            if (!string.IsNullOrEmpty(userId))
            {
                var tierResult = await loyaltyService.ResolveTierDiscountForOrderAsync(
                    userId, rawSubtotal, cancellationToken);
                tierDiscount = tierResult.DiscountAmount > 0m ? tierResult.DiscountAmount : 0m;
                if (tierResult.TierAtPurchase.HasValue)
                {
                    var tierConfig = await loyaltyTierConfigRepository.GetByTierAsync(
                        tierResult.TierAtPurchase.Value, cancellationToken);
                    tierMinOrderAmount = tierConfig?.MinimumOrderAmountForDiscount;
                }

                var activeMembership = await userMembershipRepository
                    .GetActiveForUserAsync(userId, cancellationToken);
                if (activeMembership != null)
                {
                    membershipDiscount = rawSubtotal
                        * (activeMembership.MembershipPlan.DiscountPercentage / 100m);
                }
            }

            // LOY-003 — additive Plus + tier with 12% cap. Promo isn't in the
            // quote (entered at checkout step), so we always go through the
            // "no promo wins" branch here. CreateOrder.Handler re-runs the
            // same math with promo included at submit time.
            var resolution = OrderFactory.ResolveLoy003Discount(
                membershipDiscount, tierDiscount, promoDiscount: 0m, rawSubtotal: rawSubtotal);

            var finalPrice = BookingPolicy.ApplyExpressSurcharge(
                rawSubtotal - resolution.TotalAmount, result.ExpressSurchargeApplied);

            var applied = resolution.AsChargedAgainst(result.ExpressSurchargeApplied);

            // Pick the enum that best describes what's actually showing.
            // Combined = both Plus and tier non-zero (after capping).
            // Membership = only Plus. Tier = only tier. None = neither.
            var source = (applied.MembershipAmount, applied.TierAmount) switch
            {
                ( > 0m, > 0m) => AppliedDiscountSource.Combined,
                ( > 0m, _) => AppliedDiscountSource.Membership,
                (_, > 0m) => AppliedDiscountSource.Tier,
                _ => AppliedDiscountSource.None,
            };

            return BusinessResult.Success(new Response(
                TotalPrice: grossSubtotal,
                FinalPriceAfterDiscount: finalPrice,
                OriginalSubtotal: finalPrice + applied.TotalAmount,
                AppliedDiscountSource: source,
                TierDiscountAmount: applied.TierAmount > 0m ? applied.TierAmount : null,
                MembershipDiscountAmount: applied.MembershipAmount > 0m ? applied.MembershipAmount : null,
                TierDiscountMinOrderAmount: tierMinOrderAmount,
                CurrencyId: result.CurrencyId,
                CurrencyCode: result.CurrencyCode,
                ServicesSubtotal: result.ServicesSubtotal,
                PackagesSubtotal: result.PackagesSubtotal,
                ExtrasSubtotal: result.ExtrasSubtotal,
                ExpressSurchargeApplied: result.ExpressSurchargeApplied,
                ExpressSurchargeAmount: result.ExpressSurchargeAmount,
                ExchangeRate: result.ExchangeRate,
                ExpressSurchargeWaivedByMembership: result.ExpressSurchargeWaivedByMembership,
                ExpressUpgradesRemaining: result.ExpressUpgradesRemaining));
        }
    }
}
