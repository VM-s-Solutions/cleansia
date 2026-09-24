using Cleansia.Core.Domain.Orders;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.AppServices.Tenancy;
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
        DateTime? CleaningDate = null,
        /// <summary>
        /// The country of the service address, once the wizard has one. The quote is priced in that
        /// country's currency (owner ruling 2026-09-12) unless <see cref="CurrencyId"/> names one
        /// explicitly; null with no currency is the platform default, which is what a quote taken
        /// before the address step gets.
        /// </summary>
        string? CountryId = null) : ICommand<Response>, IOperatorScopedRequest;

    /// <summary>
    /// Quote response. <see cref="TotalPrice"/> is the undiscounted total INCLUDING any express surcharge
    /// and is what create-order validates against — <b>clients must submit this value unchanged</b>.
    /// <see cref="FinalPriceAfterDiscount"/> is the display price; <see cref="OriginalSubtotal"/> equals
    /// <see cref="TotalPrice"/> because the discount is reported against the charged price. <b>Two fields
    /// that disagreed were the express-composition defect.</b> Promo is not included — it is entered at
    /// checkout and applied at create time. → /product/business-rules#price-stages
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
        /// <summary>
        /// The slot IS express and the surcharge was nevertheless not charged, because the member has a
        /// free express upgrade left. Without this field <c>ExpressSurchargeApplied: false</c> is
        /// ambiguous between "waived" and "not an express slot at all", and the wizard cannot show the
        /// waiver in place of the surcharge.
        /// </summary>
        /// <summary>
        /// How long the selection is expected to take, and how many cleaners that
        /// implies. Both are the SAME definitions the order uses —
        /// <c>OrderDuration.EstimateMinutes</c> and <c>ceil(minutes / 120)</c> — so
        /// a quote cannot promise a crew the booking will not send.
        ///
        /// Added because the home-page calculator states them under the price and
        /// had nothing to state them from; a number derived on the client would be
        /// a second implementation of a rule that already has one.
        /// </summary>
        int EstimatedDurationMinutes = 0,
        int RequiredEmployees = 1,
        bool ExpressSurchargeWaivedByMembership = false,
        /// <summary>
        /// Free express upgrades left this calendar month BEFORE this booking — server-computed, never
        /// client-counted (a client that counts its own orders disagrees with the server the first time a
        /// cancellation releases a slot). Null when the caller has no membership.
        /// </summary>
        int? ExpressUpgradesRemaining = null,
        /// <summary>
        /// The caller's spendable credit balance, and the share of an order credit may settle. The two
        /// INPUTS to the cap, not the answer — deliberately.
        ///
        /// <para>The cap is a function of the CHARGED price, and the charged price is not final at
        /// quote time: a promo code is entered at checkout and applied at create time, so a quote that
        /// returned an applicable AMOUNT would be a second answer that disagrees with the first the
        /// moment a promo lands. That is the shape of the express-composition defect this response's
        /// own docstring records. The wizard applies the rule against the price it is displaying,
        /// through the one shared function that mirrors <c>BookingPolicy.CapCreditForOrder</c> — the
        /// same arrangement <c>composeFinalPriceForUnquotedDiscount</c> already uses, and for the same
        /// reason.</para>
        ///
        /// <para>Zero for an anonymous visitor, for a customer who has never been credited, and when
        /// the balance is held in another currency. It is a PREVIEW: nothing is debited until the
        /// order is created, and the order's own <c>CreditAppliedAmount</c> is what actually
        /// happened.</para>
        /// </summary>
        decimal CreditBalance = 0m,
        decimal CreditMaxShareOfOrder = 0m,
        /// <summary>
        /// The rows the subtotals are made of, in the charge currency, so the wizard can
        /// show WHERE a number came from. A service listed at 500 in a four-room, three-
        /// bathroom flat charges 1550, and a summary that shows only the 1550 reads as a
        /// mistake.
        ///
        /// Server-side because the catalogue's per-room price reaches the client
        /// unscaled, in the BASE currency, while every figure here is in the charge
        /// currency — so the same arithmetic done in a browser is silently wrong at any
        /// exchange rate but 1.
        /// </summary>
        IReadOnlyList<QuoteLine>? Lines = null);

    /// <param name="Kind">"package", "service" or "extra".</param>
    /// <param name="ItemId">Package/Service id, or an Extra's slug. The client holds the
    /// catalogue already and resolves the name from it, in the customer's language.</param>
    public record QuoteLine(
        string Kind,
        string ItemId,
        decimal BaseAmount,
        decimal UnitAmount,
        int Units,
        decimal Amount);

    public class Validator : AbstractValidator<Command>
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

            RuleFor(x => x.Rooms)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive)
                .LessThanOrEqualTo(BookingPolicy.MaxRooms)
                .WithMessage(BusinessErrorMessage.OrderSizeExceedsMaximum);

            RuleFor(x => x.Bathrooms)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive)
                .LessThanOrEqualTo(BookingPolicy.MaxBathrooms)
                .WithMessage(BusinessErrorMessage.OrderSizeExceedsMaximum);

            // Existence, then a price row in the currency being quoted in. The second term reuses the
            // selection code deliberately -- see CreateOrder.Validator: an entry with no row in this
            // currency is one the wizard never offered in this market, and the calculator throws on it.
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

            // ONE ordered chain, the same discipline as CreateOrder's price chain: the class cascade is
            // Continue, so the country and currency rules have to head the chain that ends in anything
            // priced in that currency. The country must be one the platform operates in; the currency
            // it resolves to -- the caller's when named, else the country's, else the platform default
            // -- must be one the platform can quote in: switched on AND priced
            // (ICurrencyRepository.IsOfferableAsync). "Exists" was the pre-Wave-A rule and was the hole:
            // every seeded currency existed. Offerable is the property that was missing. The default is
            // not looked up: a quote with neither field is what every wizard opens with.
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(CountryIsServicedAsync)
                .WithMessage(BusinessErrorMessage.CountryNotServiced)
                .WithErrorCode(nameof(Command.CountryId))
                .MustAsync(CurrencyIsOfferableAsync)
                .WithMessage(BusinessErrorMessage.InvalidCurrency)
                .WithErrorCode(nameof(Command.CurrencyId))
                .MustAsync(SpanWithinCapAsync)
                .WithMessage(BusinessErrorMessage.OrderSpanExceedsMaximum);
        }

        private const string CountryServicedKey = "quoteOrder.countryServiced";

        /// <summary>
        /// Cached on the context because the two item chains ask it before the chain that owns the
        /// refusal does: the resolver throws on a country it cannot resolve, and an unserviced country
        /// has no currency to ask for -- the item rules yield to <c>CountryNotServiced</c> instead.
        /// </summary>
        private async Task<bool> CountryIsServicedAsync(
            Command command, Command _, ValidationContext<Command> context, CancellationToken cancellationToken)
        {
            if (context.RootContextData.TryGetValue(CountryServicedKey, out var cached) && cached is bool serviced)
            {
                return serviced;
            }

            serviced = string.IsNullOrEmpty(command.CountryId)
                       || await _countryRepository.IsServicedAsync(command.CountryId, cancellationToken);
            context.RootContextData[CountryServicedKey] = serviced;
            return serviced;
        }

        private async Task<bool> CurrencyIsOfferableAsync(
            Command command,
            Command _,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
        {
            var currencyId = await ResolveQuoteCurrencyIdAsync(command, context, cancellationToken);
            return currencyId is null
                   || await _currencyRepository.IsOfferableAsync(currencyId, cancellationToken);
        }

        private async Task<bool> ArePricedInQuoteCurrencyAsync(
            Command command,
            IEnumerable<string> serviceIds,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
        {
            var ids = serviceIds.Distinct().ToList();
            if (ids.Count == 0 || !await CountryIsServicedAsync(command, command, context, cancellationToken))
            {
                return true;
            }
            var currencyId = await ResolveQuoteCurrencyIdAsync(command, context, cancellationToken)
                             ?? (await _currencyRepository.GetDefaultAsync(cancellationToken)).Id;
            var prices = await CataloguePriceLookup.ForServicesAsync(
                _servicePriceRepository, ids, currencyId, cancellationToken);
            return ids.All(prices.ContainsKey);
        }

        private async Task<bool> ArePackagesPricedInQuoteCurrencyAsync(
            Command command,
            IEnumerable<string> packageIds,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
        {
            var ids = packageIds.Distinct().ToList();
            if (ids.Count == 0 || !await CountryIsServicedAsync(command, command, context, cancellationToken))
            {
                return true;
            }
            var currencyId = await ResolveQuoteCurrencyIdAsync(command, context, cancellationToken)
                             ?? (await _currencyRepository.GetDefaultAsync(cancellationToken)).Id;
            var prices = await CataloguePriceLookup.ForPackagesAsync(
                _packagePriceRepository, ids, currencyId, cancellationToken);
            return ids.All(prices.ContainsKey);
        }

        private const string QuoteCurrencyIdKey = "quoteOrder.currencyId";

        /// <summary>
        /// The same resolution the handler prices with -- <see cref="ResolveQuoteCurrencyId"/> -- cached
        /// on the validation context because three rules ask for it. Null means the platform default,
        /// left to the calculator, exactly as the handler leaves it.
        /// </summary>
        private async Task<string?> ResolveQuoteCurrencyIdAsync(
            Command command, ValidationContext<Command> context, CancellationToken cancellationToken)
        {
            if (context.RootContextData.TryGetValue(QuoteCurrencyIdKey, out var cached))
            {
                return cached as string;
            }

            var currencyId = await ResolveQuoteCurrencyId(
                command.CurrencyId, command.CountryId, _currencyResolutionService, cancellationToken);
            context.RootContextData[QuoteCurrencyIdKey] = currencyId;
            return currencyId;
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

    /// <summary>
    /// THE CURRENCY A QUOTE IS PRICED IN: the caller's when named, else the country's (the service
    /// address's, owner ruling 2026-09-12), else null for the platform default. One function because
    /// the validator and the handler must resolve identically, and because <see cref="QuotePlusSavings"/>
    /// asks the same question of the same two fields. A named currency wins so a client that echoes the
    /// currency it was quoted in keeps agreeing with itself; the country decides only when the client
    /// leaves the choice to the server, which every shipped client does.
    /// </summary>
    internal static async Task<string?> ResolveQuoteCurrencyId(
        string? currencyId,
        string? countryId,
        ICurrencyResolutionService currencyResolutionService,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(currencyId))
        {
            return currencyId;
        }
        if (string.IsNullOrEmpty(countryId))
        {
            return null;
        }
        return (await currencyResolutionService.ResolveCurrencyForCountryAsync(countryId, cancellationToken)).Id;
    }

    public class Handler(
        IOrderPricingCalculator pricingCalculator,
        IUserSessionProvider userSessionProvider,
        ILoyaltyService loyaltyService,
        IUserMembershipRepository userMembershipRepository,
        ICreditAccountRepository creditAccountRepository,
        ICurrencyResolutionService currencyResolutionService)
        : ICommandHandler<Command, Response>
    {
        /// <summary>
        /// What the caller has to spend, in the currency this quote is priced in. Zero for an
        /// anonymous visitor, for a customer with no account, and when the balance is held in another
        /// currency — the same gates the checkout applies, minus the payment-type one, because the
        /// quote is taken before the customer has chosen card or cash.
        ///
        /// <para>That omission is why this is a preview rather than a promise: a customer who then
        /// picks cash sees the credit line disappear at the payment step, which is correct and which
        /// the wizard can explain.</para>
        /// </summary>
        private async Task<decimal> ResolveCreditBalanceAsync(
            string currencyId, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return 0m;
            }

            // The quote must show the balance the CHECKOUT will actually spend, so it asks the same
            // question CreateOrder does, keyed the same way.
            var spendable = await creditAccountRepository.GetSpendableAsync(
                userId, currencyId, cancellationToken);
            return spendable != null && spendable.CurrencyId == currencyId ? spendable.Balance : 0m;
        }

        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var currencyId = await ResolveQuoteCurrencyId(
                command.CurrencyId, command.CountryId, currencyResolutionService, cancellationToken);
            var result = await pricingCalculator.CalculateAsync(
                command.SelectedServiceIds,
                command.SelectedPackageIds,
                command.SelectedExtraSlugs ?? Array.Empty<string>(),
                command.Rooms,
                command.Bathrooms,
                // Validated offerable above; null is the platform default. Safe to honour because
                // nothing converts -- a currency selects which price ROWS are read, it no longer scales
                // the CZK catalogue by a stored rate (the Wave A hole).
                currencyId,
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
                    userId, rawSubtotal, result.CurrencyId, cancellationToken);
                tierDiscount = tierResult.DiscountAmount > 0m ? tierResult.DiscountAmount : 0m;
                // The floor the ORDER will judge, in the order's currency — null when none applies, so
                // the wizard never states a 1000 CZK floor over a EUR price.
                tierMinOrderAmount = tierResult.MinimumOrderAmount;

                var activeMembership = await userMembershipRepository
                    .GetEntitledForUserAsync(userId, cancellationToken);
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

            var estimatedMinutes = result.EstimatedDurationMinutes;
            var requiredEmployees = OrderDuration.RequiredEmployees(estimatedMinutes);

            var creditBalance = await ResolveCreditBalanceAsync(result.CurrencyId, cancellationToken);

            return BusinessResult.Success(new Response(
                TotalPrice: grossSubtotal,
                FinalPriceAfterDiscount: finalPrice,
                OriginalSubtotal: finalPrice + applied.TotalAmount,
                AppliedDiscountSource: source,
                TierDiscountAmount: applied.TierAmount > 0m ? applied.TierAmount : null,
                MembershipDiscountAmount: applied.MembershipAmount > 0m ? applied.MembershipAmount : null,
                TierDiscountMinOrderAmount: tierMinOrderAmount,
                CreditBalance: creditBalance,
                CreditMaxShareOfOrder: BookingPolicy.MaxCreditShareOfOrder,
                CurrencyId: result.CurrencyId,
                CurrencyCode: result.CurrencyCode,
                ServicesSubtotal: result.ServicesSubtotal,
                PackagesSubtotal: result.PackagesSubtotal,
                ExtrasSubtotal: result.ExtrasSubtotal,
                ExpressSurchargeApplied: result.ExpressSurchargeApplied,
                ExpressSurchargeAmount: result.ExpressSurchargeAmount,
                EstimatedDurationMinutes: estimatedMinutes,
                RequiredEmployees: requiredEmployees,
                Lines: (result.Lines ?? [])
                    .Select(l => new QuoteLine(l.Kind, l.ItemId, l.BaseAmount, l.UnitAmount, l.Units, l.Amount))
                    .ToList(),
                ExpressSurchargeWaivedByMembership: result.ExpressSurchargeWaivedByMembership,
                ExpressUpgradesRemaining: result.ExpressUpgradesRemaining));
        }
    }
}
