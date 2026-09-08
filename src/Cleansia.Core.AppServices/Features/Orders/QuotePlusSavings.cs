using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// What this basket would cost with a Cleansia Plus plan the caller does not have.
///
/// It answers one question the booking wizard's Plus step asks — "you would save X on
/// today's order" — and it answers it HERE because a browser cannot. Three of the four
/// steps are invisible from the client: the 12% combined cap on membership + tier has no
/// client-side representation, the tier amount the quote already reports arrives
/// grossed-up by the express surcharge so a client would have to un-gross it, cap, and
/// re-gross, and the express waiver a member gets is not granted during the Stripe trial
/// a wizard subscriber would start in.
///
/// It quotes the DISCOUNT ONLY, deliberately. Adding the value of a waived express
/// surcharge would produce a bigger number that is false for exactly the customer this
/// screen is talking to — one subscribing today, and therefore trialing.
///
/// A separate query rather than a field on <see cref="QuoteOrder"/>: the wizard asks
/// this once, on one step, for one plan, and every other quote consumer would pay for it
/// on every keystroke.
/// </summary>
public static class QuotePlusSavings
{
    public record Query(
        IEnumerable<string> SelectedServiceIds,
        IEnumerable<string> SelectedPackageIds,
        int Rooms,
        int Bathrooms,
        string PlanCode,
        string? CurrencyId = null,
        IEnumerable<string>? SelectedExtraSlugs = null,
        DateTime? CleaningDate = null) : IQuery<Response>;

    public record Response(
        /// <summary>What the plan's discount is worth on this basket, in the charge currency.</summary>
        decimal WouldSaveAmount,
        /// <summary>What the basket would total with it applied.</summary>
        decimal WouldPayTotal,
        decimal CurrentTotal,
        string CurrencyCode,
        string PlanCode);

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.PlanCode)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.Rooms).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Bathrooms).GreaterThanOrEqualTo(0);
        }
    }

    public class Handler(
        IOrderPricingCalculator pricingCalculator,
        IMembershipPlanRepository membershipPlanRepository,
        ILoyaltyService loyaltyService,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var plan = await membershipPlanRepository.GetByCodeAsync(query.PlanCode, cancellationToken);
            if (plan is null || !plan.IsActive)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(query.PlanCode), BusinessErrorMessage.MembershipPlanNotFound));
            }

            var nowUtc = DateTime.UtcNow;
            var userId = userSessionProvider.GetUserId();

            var result = await pricingCalculator.CalculateAsync(
                query.SelectedServiceIds,
                query.SelectedPackageIds,
                query.SelectedExtraSlugs ?? [],
                query.Rooms,
                query.Bathrooms,
                // currencyId: null -- the server resolves it, never the caller. Accepting one let any
                // authenticated caller name a currency and have the whole CZK catalogue multiplied by
                // its stored rate. Wave B replaces this with resolution from the address country.
                null,
                query.CleaningDate,
                userId,
                nowUtc,
                cancellationToken);

            // The same two bases QuoteOrder uses: the gross is what the client resubmits,
            // the discount is resolved on the raw pre-surcharge subtotal.
            var grossSubtotal = result.TotalPrice;
            var rawSubtotal = grossSubtotal - result.ExpressSurchargeAmount;

            // The caller's REAL tier, because the cap is on the pair. Previewing the
            // membership share alone would overstate it for a customer who already has a
            // tier discount big enough to eat the headroom.
            decimal tierDiscount = 0m;
            if (!string.IsNullOrEmpty(userId))
            {
                var tierResult = await loyaltyService.ResolveTierDiscountForOrderAsync(
                    userId, rawSubtotal, cancellationToken);
                tierDiscount = tierResult.DiscountAmount > 0m ? tierResult.DiscountAmount : 0m;
            }

            var membershipDiscount = rawSubtotal * (plan.DiscountPercentage / 100m);

            // The existing resolver, so the 12% cap and the proportional split keep their
            // one home; and the existing gross-up, so the reported saving is what the
            // receipt would show rather than a pre-surcharge figure.
            var withPlus = OrderFactory.ResolveLoy003Discount(
                membershipDiscount, tierDiscount, promoDiscount: 0m, rawSubtotal);
            var withoutPlus = OrderFactory.ResolveLoy003Discount(
                membershipDiscount: 0m, tierDiscount, promoDiscount: 0m, rawSubtotal);

            var surchargeApplies = result.ExpressSurchargeApplied;
            var payWithPlus = BookingPolicy.ApplyExpressSurcharge(
                rawSubtotal - withPlus.TotalAmount, surchargeApplies);
            var payToday = BookingPolicy.ApplyExpressSurcharge(
                rawSubtotal - withoutPlus.TotalAmount, surchargeApplies);

            // The DIFFERENCE the plan makes, not the plan's discount in isolation: where
            // the cap binds, part of the membership discount displaces tier discount the
            // customer was getting anyway, and only the remainder is a saving.
            var saving = payToday - payWithPlus;

            return BusinessResult.Success(new Response(
                WouldSaveAmount: saving > 0m ? decimal.Round(saving, 2, MidpointRounding.AwayFromZero) : 0m,
                WouldPayTotal: decimal.Round(payWithPlus, 2, MidpointRounding.AwayFromZero),
                CurrentTotal: decimal.Round(payToday, 2, MidpointRounding.AwayFromZero),
                CurrencyCode: result.CurrencyCode,
                PlanCode: plan.Code));
        }
    }
}
