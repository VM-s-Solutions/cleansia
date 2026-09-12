using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// What this basket would cost with a Cleansia Plus plan the caller does not have.
///
/// It answers one question the booking wizard's Plus step asks — "you would save X on
/// today's order" — and it answers it HERE because a browser cannot. Three of the four
/// steps are invisible from the client: the 12% combined cap on membership + tier has no
/// client-side representation, the tier amount the quote already reports arrives
/// grossed-up by the express surcharge so a client would have to un-gross it, cap, and
/// re-gross, and the express waivers a member gets are metered per month, so whether one
/// would apply to today's order is not knowable from the price alone.
///
/// It quotes the DISCOUNT ONLY, deliberately. Adding the value of a waived express
/// surcharge would produce a bigger number that may be false for exactly the customer this
/// screen is talking to — one who has not subscribed yet and holds no waiver.
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
        DateTime? CleaningDate = null,
        /// <summary>The service address's country -- see <see cref="QuoteOrder.Command.CountryId"/>.</summary>
        string? CountryId = null) : IQuery<Response>;

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
        private readonly IServiceRepository _serviceRepository;
        private readonly IPackageRepository _packageRepository;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly ICountryRepository _countryRepository;
        private readonly ICurrencyResolutionService _currencyResolutionService;
        private readonly IServicePriceRepository _servicePriceRepository;
        private readonly IPackagePriceRepository _packagePriceRepository;

        public Validator(
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository,
            ICurrencyRepository currencyRepository,
            ICountryRepository countryRepository,
            ICurrencyResolutionService currencyResolutionService,
            IServicePriceRepository servicePriceRepository,
            IPackagePriceRepository packagePriceRepository)
        {
            _serviceRepository = serviceRepository;
            _packageRepository = packageRepository;
            _currencyRepository = currencyRepository;
            _countryRepository = countryRepository;
            _currencyResolutionService = currencyResolutionService;
            _servicePriceRepository = servicePriceRepository;
            _packagePriceRepository = packagePriceRepository;

            RuleFor(x => x.PlanCode)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.Rooms).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Bathrooms).GreaterThanOrEqualTo(0);

            // Existence, then a price row in the currency being quoted in -- the same two terms as
            // QuoteOrder, because this query prices the same basket and the calculator throws on an
            // entry with no row in the resolved currency.
            RuleFor(x => x.SelectedServiceIds)
                .Cascade(CascadeMode.Stop)
                .MustAsync(serviceRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices)
                .MustAsync(ArePricedInQuoteCurrencyAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices);

            RuleFor(x => x.SelectedPackageIds)
                .Cascade(CascadeMode.Stop)
                .MustAsync(packageRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage)
                .MustAsync(ArePackagesPricedInQuoteCurrencyAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage);

            // Same rules, same keys, same resolution as QuoteOrder: this query prices the basket too,
            // and the calculator throws on a currency it cannot price in.
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(CountryIsServicedAsync)
                .WithMessage(BusinessErrorMessage.CountryNotServiced)
                .WithErrorCode(nameof(Query.CountryId))
                .MustAsync(CurrencyIsOfferableAsync)
                .WithMessage(BusinessErrorMessage.InvalidCurrency)
                .WithErrorCode(nameof(Query.CurrencyId))
                .MustAsync(SpanWithinCapAsync)
                .WithMessage(BusinessErrorMessage.OrderSpanExceedsMaximum);
        }

        private async Task<bool> CountryIsServicedAsync(Query query, CancellationToken cancellationToken)
            => string.IsNullOrEmpty(query.CountryId)
               || await _countryRepository.IsServicedAsync(query.CountryId, cancellationToken);

        /// <summary>
        /// The same cap QuoteOrder draws (ADR-0039 D3.4): a preview must not show savings on a basket the
        /// booking will refuse on span. An empty selection still previews, as it still quotes.
        /// </summary>
        private async Task<bool> SpanWithinCapAsync(Query query, CancellationToken cancellationToken)
        {
            var serviceMinutes = await _serviceRepository
                .GetByIds(query.SelectedServiceIds)
                .SumAsync(s => s.EstimatedTime, cancellationToken);

            var packagedServiceMinutes = await _packageRepository
                .GetByIds(query.SelectedPackageIds)
                .SelectMany(p => p.IncludedServices)
                .SumAsync(ps => ps.Service!.EstimatedTime, cancellationToken);

            return !BookingPolicy.ExceedsMaxBookableSpan(serviceMinutes + packagedServiceMinutes);
        }

        private async Task<bool> CurrencyIsOfferableAsync(
            Query query,
            Query _,
            ValidationContext<Query> context,
            CancellationToken cancellationToken)
        {
            var currencyId = await ResolveQuoteCurrencyIdAsync(query, context, cancellationToken);
            return currencyId is null
                   || await _currencyRepository.IsOfferableAsync(currencyId, cancellationToken);
        }

        private async Task<bool> ArePricedInQuoteCurrencyAsync(
            Query query,
            IEnumerable<string> serviceIds,
            ValidationContext<Query> context,
            CancellationToken cancellationToken)
        {
            var ids = serviceIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return true;
            }
            var currencyId = await ResolveQuoteCurrencyIdAsync(query, context, cancellationToken)
                             ?? (await _currencyRepository.GetDefaultAsync(cancellationToken)).Id;
            var prices = await CataloguePriceLookup.ForServicesAsync(
                _servicePriceRepository, ids, currencyId, cancellationToken);
            return ids.All(prices.ContainsKey);
        }

        private async Task<bool> ArePackagesPricedInQuoteCurrencyAsync(
            Query query,
            IEnumerable<string> packageIds,
            ValidationContext<Query> context,
            CancellationToken cancellationToken)
        {
            var ids = packageIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return true;
            }
            var currencyId = await ResolveQuoteCurrencyIdAsync(query, context, cancellationToken)
                             ?? (await _currencyRepository.GetDefaultAsync(cancellationToken)).Id;
            var prices = await CataloguePriceLookup.ForPackagesAsync(
                _packagePriceRepository, ids, currencyId, cancellationToken);
            return ids.All(prices.ContainsKey);
        }

        private const string QuoteCurrencyIdKey = "quotePlusSavings.currencyId";

        private async Task<string?> ResolveQuoteCurrencyIdAsync(
            Query query, ValidationContext<Query> context, CancellationToken cancellationToken)
        {
            if (context.RootContextData.TryGetValue(QuoteCurrencyIdKey, out var cached))
            {
                return cached as string;
            }

            var currencyId = await QuoteOrder.ResolveQuoteCurrencyId(
                query.CurrencyId, query.CountryId, _currencyResolutionService, cancellationToken);
            context.RootContextData[QuoteCurrencyIdKey] = currencyId;
            return currencyId;
        }
    }

    public class Handler(
        IOrderPricingCalculator pricingCalculator,
        IMembershipPlanRepository membershipPlanRepository,
        ILoyaltyService loyaltyService,
        IUserSessionProvider userSessionProvider,
        ICurrencyResolutionService currencyResolutionService)
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
                // The same resolution as QuoteOrder, validated offerable; null is the platform default.
                await QuoteOrder.ResolveQuoteCurrencyId(
                    query.CurrencyId, query.CountryId, currencyResolutionService, cancellationToken),
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
                    userId, rawSubtotal, result.CurrencyId, cancellationToken);
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
