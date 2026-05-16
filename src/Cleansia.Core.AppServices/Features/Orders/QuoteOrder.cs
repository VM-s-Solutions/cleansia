using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

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
    /// Quote response. <see cref="TotalPrice"/> is the RAW subtotal (matches
    /// <c>IOrderPricingCalculator.CalculateAsync</c>) and is what
    /// <c>CreateOrder.PriceMatchesAsync</c> validates against — clients must
    /// submit this value unchanged. <see cref="FinalPriceAfterDiscount"/> is the
    /// display price after the best-of-three (tier vs membership) discount.
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
        decimal ExchangeRate);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository,
            ICurrencyRepository currencyRepository)
        {
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
            var result = await pricingCalculator.CalculateAsync(
                command.SelectedServiceIds,
                command.SelectedPackageIds,
                command.SelectedExtraSlugs ?? Array.Empty<string>(),
                command.Rooms,
                command.Bathrooms,
                command.CurrencyId,
                command.CleaningDate,
                cancellationToken);

            var subtotal = result.TotalPrice;
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
                    userId, subtotal, cancellationToken);
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
                    membershipDiscount = subtotal
                        * (activeMembership.MembershipPlan.DiscountPercentage / 100m);
                }
            }

            // LOY-003 — additive Plus + tier with 12% cap. Promo isn't in the
            // quote (entered at checkout step), so we always go through the
            // "no promo wins" branch here. CreateOrder.Handler re-runs the
            // same math with promo included at submit time.
            var resolution = OrderFactory.ResolveLoy003Discount(
                membershipDiscount, tierDiscount, promoDiscount: 0m, rawSubtotal: subtotal);

            // Pick the enum that best describes what's actually showing.
            // Combined = both Plus and tier non-zero (after capping).
            // Membership = only Plus. Tier = only tier. None = neither.
            var source = (resolution.MembershipAmount, resolution.TierAmount) switch
            {
                ( > 0m, > 0m) => AppliedDiscountSource.Combined,
                ( > 0m, _) => AppliedDiscountSource.Membership,
                (_, > 0m) => AppliedDiscountSource.Tier,
                _ => AppliedDiscountSource.None,
            };

            return BusinessResult.Success(new Response(
                TotalPrice: subtotal,
                FinalPriceAfterDiscount: subtotal - resolution.TotalAmount,
                OriginalSubtotal: subtotal,
                AppliedDiscountSource: source,
                TierDiscountAmount: resolution.TierAmount > 0m ? resolution.TierAmount : null,
                MembershipDiscountAmount: resolution.MembershipAmount > 0m ? resolution.MembershipAmount : null,
                TierDiscountMinOrderAmount: tierMinOrderAmount,
                CurrencyId: result.CurrencyId,
                CurrencyCode: result.CurrencyCode,
                ServicesSubtotal: result.ServicesSubtotal,
                PackagesSubtotal: result.PackagesSubtotal,
                ExtrasSubtotal: result.ExtrasSubtotal,
                ExpressSurchargeApplied: result.ExpressSurchargeApplied,
                ExpressSurchargeAmount: result.ExpressSurchargeAmount,
                ExchangeRate: result.ExchangeRate));
        }
    }
}
