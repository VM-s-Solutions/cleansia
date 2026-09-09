using Cleansia.Core.Domain.Orders;
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
        decimal ExchangeRate,
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

        public Validator(
            IServiceRepository serviceRepository,
            IPackageRepository packageRepository)
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

            // No currency rule. The field stays on the wire (removing it costs an NSwag run on three
            // clients and a mobile spec re-dump for no behaviour change) but the server ignores it
            // entirely, so there is nothing left to validate. Validating that a caller-named currency
            // EXISTS was never the safety property anyway -- every seeded currency existed, and that
            // was exactly the hole.

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
        IServiceRepository serviceRepository,
        IPackageRepository packageRepository,
        IUserSessionProvider userSessionProvider,
        ILoyaltyService loyaltyService,
        ILoyaltyTierConfigRepository loyaltyTierConfigRepository,
        IUserMembershipRepository userMembershipRepository,
        ICreditAccountRepository creditAccountRepository)
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
            var result = await pricingCalculator.CalculateAsync(
                command.SelectedServiceIds,
                command.SelectedPackageIds,
                command.SelectedExtraSlugs ?? Array.Empty<string>(),
                command.Rooms,
                command.Bathrooms,
                // currencyId: null -- the server resolves it, never the caller. Accepting one let any
                // authenticated caller name a currency and have the whole CZK catalogue multiplied by
                // its stored rate. Wave B replaces this with resolution from the address country.
                null,
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

            // The same two definitions the order uses. Loaded here rather than
            // derived on the client, so the number under the price on the home page
            // and the crew the booking actually sends cannot disagree.
            var services = await serviceRepository
                .GetByIds(command.SelectedServiceIds)
                .ToListAsync(cancellationToken);
            var packages = await packageRepository
                .GetByIds(command.SelectedPackageIds)
                .Include(p => p.IncludedServices)
                .ThenInclude(i => i.Service)
                .ToListAsync(cancellationToken);

            var estimatedMinutes = OrderDuration.EstimateMinutes(services, packages);
            var requiredEmployees = estimatedMinutes <= 0
                ? 1
                : (int)Math.Ceiling(estimatedMinutes / (double)OrderDuration.MinutesPerEmployee);

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
                ExchangeRate: result.ExchangeRate,
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
