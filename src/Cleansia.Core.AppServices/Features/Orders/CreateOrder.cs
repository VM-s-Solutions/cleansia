using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

[AuditAction("customer.order.create", Audience = AuditAudience.Customer, ResourceType = "Order", AllowsAnonymousActor = true)]
public class CreateOrder
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IPackageRepository _packageRepository;
        private readonly IServiceRepository _serviceRepository;
        private readonly IOrderPricingCalculator _pricingCalculator;
        private readonly IOrderRepository _orderRepository;
        private readonly IUserMembershipRepository _userMembershipRepository;
        private readonly IUserSessionProvider _userSessionProvider;
        private readonly IEmployeePayConfigRepository _payConfigRepository;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly IOrderAddressResolver _orderAddressResolver;
        private readonly ICurrencyResolutionService _currencyResolutionService;
        private readonly IServicePriceRepository _servicePriceRepository;
        private readonly IPackagePriceRepository _packagePriceRepository;
        private readonly IPromoCodeService _promoCodeService;
        private readonly IOperatorTenantResolver _operatorTenantResolver;
        private readonly ITenantProvider _tenantProvider;

        public Validator(
            IPackageRepository packageRepository,
            IServiceRepository serviceRepository,
            IOrderPricingCalculator pricingCalculator,
            IOrderRepository orderRepository,
            IUserMembershipRepository userMembershipRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeePayConfigRepository payConfigRepository,
            ICurrencyRepository currencyRepository,
            IOrderAddressResolver orderAddressResolver,
            ICurrencyResolutionService currencyResolutionService,
            IServicePriceRepository servicePriceRepository,
            IPackagePriceRepository packagePriceRepository,
            IPromoCodeService promoCodeService,
            IOperatorTenantResolver operatorTenantResolver,
            ITenantProvider tenantProvider,
            ILanguageRepository languageRepository)
        {
            _operatorTenantResolver = operatorTenantResolver;
            _tenantProvider = tenantProvider;
            _packageRepository = packageRepository;
            _serviceRepository = serviceRepository;
            _payConfigRepository = payConfigRepository;
            _currencyRepository = currencyRepository;
            _orderAddressResolver = orderAddressResolver;
            _currencyResolutionService = currencyResolutionService;
            _servicePriceRepository = servicePriceRepository;
            _packagePriceRepository = packagePriceRepository;
            _promoCodeService = promoCodeService;
            _pricingCalculator = pricingCalculator;
            _orderRepository = orderRepository;
            _userMembershipRepository = userMembershipRepository;
            _userSessionProvider = userSessionProvider;

            RuleFor(x => x.Language)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Language))
                .SetValidator(new LanguageValidator(languageRepository));

            RuleFor(x => x.PaymentType)
                .IsInEnum().WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.CustomerName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(2)
                .WithMessage(BusinessErrorMessage.MinLength)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.CustomerEmail)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .EmailAddress()
                .WithMessage(BusinessErrorMessage.InvalidEmailFormat)
                .MaximumLength(150)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.CustomerPhone)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(20)
                .WithMessage(BusinessErrorMessage.MaxLength);

            When(x => x.CustomerAddress != null, () =>
            {
                RuleFor(x => x.CustomerAddress!.Street)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .WithMessage(BusinessErrorMessage.Required)
                    .MinimumLength(5)
                    .WithMessage(BusinessErrorMessage.MinLength)
                    .MaximumLength(255)
                    .WithMessage(BusinessErrorMessage.MaxLength);

                RuleFor(x => x.CustomerAddress!.City)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .WithMessage(BusinessErrorMessage.Required)
                    .MinimumLength(2)
                    .WithMessage(BusinessErrorMessage.MinLength)
                    .MaximumLength(100)
                    .WithMessage(BusinessErrorMessage.MaxLength);

                RuleFor(x => x.CustomerAddress!.ZipCode)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .WithMessage(BusinessErrorMessage.Required)
                    .MinimumLength(3)
                    .WithMessage(BusinessErrorMessage.MinLength)
                    .MaximumLength(20)
                    .WithMessage(BusinessErrorMessage.MaxLength);
            });

            RuleFor(x => x.CleaningDate)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(DateTime.UtcNow)
                .WithMessage(BusinessErrorMessage.CleaningDateInFuture)
                .Must(cleaningDate => !BookingPolicy.IsBelowMinimumLeadTime(cleaningDate, DateTime.UtcNow))
                .WithMessage(BusinessErrorMessage.CleaningDateBelowLeadTime);

            RuleFor(x => x.TotalPrice)
                .GreaterThan(0)
                .WithMessage(BusinessErrorMessage.TotalPriceMustBePositive);

            // The currency rule heads the price chain below -- it has to run BEFORE the calculator does.

            RuleFor(x => x)
                .Must(cmd => (cmd.CustomerAddress != null) ^ (!string.IsNullOrEmpty(cmd.SavedAddressId)))
                .WithMessage(BusinessErrorMessage.OrderAddressExactlyOneRequired)
                .WithName(nameof(Command.CustomerAddress));

            // TENANT AND CURRENCY ARE TWO READS OF ONE COUNTRY (ADR-0061 D6): the order's currency is
            // the service address's country's, and its tenant is the ambient one — the claim, or for a
            // guest the operator the scope behaviour resolved from the request's market. The two can
            // disagree (a customer of one operator booking an address another operates; a guest whose
            // request named no country while the address resolves to another market), and an order
            // stamped with a tenant its own account cannot list is the outcome this refuses.
            // UNCONDITIONAL — guest and authenticated alike (ADR-0061 D6).
            RuleFor(x => x)
                .MustAsync(AddressCountryIsOperatedByAmbientTenantAsync)
                .WithMessage(BusinessErrorMessage.OrderCountryOperatorMismatch)
                .WithErrorCode(nameof(Command.CustomerAddress));

            // The pay-coverage and price terms mirror OrderFactory's backstops so the customer gets a
            // 400 instead of a 500. They reuse the existing selection codes deliberately: the booking
            // wizard never offers an entry without a platform-wide pay config or a price row in the
            // currency it is browsing in, so a caller that reaches this is submitting an id it was not
            // shown -- typically one picked before the address moved the booking into another market --
            // and a dedicated customer-visible key would describe an internal condition to the wrong
            // audience.
            // Both gates are asked IN THE ORDER'S CURRENCY -- the service address's country's -- because
            // the pay writer reads only rates denominated in it and the calculator reads only price rows
            // in it. The resolution is the same the handler stamps the order with. They yield when the
            // caller NAMED another market's currency: the currency rule below refuses that with its own
            // key, and a second refusal in the address's currency would describe a market the caller
            // never asked to book in.
            RuleFor(x => x.SelectedServiceIds)
                .Cascade(CascadeMode.Stop)
                .MustAsync(serviceRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices)
                .MustAsync(HavePayCoverageAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices)
                .MustAsync(ArePricedInOrderCurrencyAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices);

            RuleFor(x => x.SelectedPackageIds)
                .Cascade(CascadeMode.Stop)
                .MustAsync(packageRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage)
                .MustAsync(HavePackagePayCoverageAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage)
                .MustAsync(ArePackagesPricedInOrderCurrencyAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage);

            // The three price rules are ORDERED and share one calculator run. The waiver rule goes first
            // because Cascade.Stop means only the first failure is reported, and a member who lost their
            // free upgrade between the quote and here must get the dedicated code — TotalPriceNotMatch is
            // rendered by every client as a generic "the price changed", which is exactly the sentence
            // that cannot explain this. The promo rule is LAST: it previews against the subtotal the
            // calculator produced, so it only has an answer once the price the customer consented to
            // has been confirmed.
            //
            // The two currency rules are FIRST, and in THIS chain rather than their own RuleFor: the
            // class-level cascade is Continue, so a separate rule would not stop the two price rules
            // below from running the calculator with the bad currency -- and the calculator throws on a
            // currency it cannot price in, which would turn a 400 into a 500. WithErrorCode keys the
            // failure to the field; the chain's other failures keep the root name.
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(CurrencyMatchesAddressCountryAsync)
                .WithMessage(BusinessErrorMessage.InvalidCurrency)
                .WithErrorCode(nameof(Command.CurrencyId))
                .MustAsync(CurrencyIsOfferableAsync)
                .WithMessage(BusinessErrorMessage.InvalidCurrency)
                .WithErrorCode(nameof(Command.CurrencyId))
                .Must(OrderMustNotBeEmpty)
                .WithMessage(BusinessErrorMessage.EmptyOrder)
                .MustAsync(SpanWithinCapAsync)
                .WithMessage(BusinessErrorMessage.OrderSpanExceedsMaximum)
                .MustAsync(ExpressWaiverStillAvailableAsync)
                .WithMessage(BusinessErrorMessage.ExpressWaiverNoLongerAvailable)
                .MustAsync(PriceMatchesAsync)
                .WithMessage(BusinessErrorMessage.TotalPriceNotMatch)
                .Must(PromoNamesASignedInCustomer)
                .WithMessage(BusinessErrorMessage.PromoRequiresAccount)
                .WithErrorCode(nameof(Command.PromoCode))
                .MustAsync(PromoWouldBeHonouredAsync)
                .WithMessage(PromoErrorTemplate)
                .WithErrorCode(nameof(Command.PromoCode));

            // Optional free-text. Null/empty passes (MaximumLength is a no-op on
            // null), so old clients that never send the field are unaffected.
            // 2000 matches the other free-text order notes (AddOrderNote,
            // ReportOrderIssue) — the column itself is unbounded `text`.
            RuleFor(x => x.SpecialInstructions)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.AccessInstructions)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLength);

            // 20 matches the column. A floor is short in every market we ship —
            // "3", "přízemí", "2A" — and a longer value is a note, not a floor.
            RuleFor(x => x.CustomerFloor)
                .MaximumLength(20)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.CustomerApartment)
                .MaximumLength(20)
                .WithMessage(BusinessErrorMessage.MaxLength);

            // The four the wizard offers. Order.AccessMode is a plain string so
            // nothing on this side has to know an enum it never compares — which
            // makes this the one place a wrong value would otherwise get in.
            RuleFor(x => x.AccessMode)
                .Must(mode => mode is null or "at_home" or "keys_handover" or "door_code" or "reception")
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            // ONE ordered chain, never a second RuleFor: the class-level default is Continue, so a
            // parallel chain would report both refusals and the client renders whichever it reads first.
            // Entitlement leads because it is the answer that reveals least — it does not depend on the
            // employee id at all, so a caller without Plus learns nothing about anyone by probing.
            When(x => !string.IsNullOrEmpty(x.PreferredEmployeeId), () =>
            {
                RuleFor(x => x)
                    .Cascade(CascadeMode.Stop)
                    .MustAsync(CallerHasActiveMembershipAsync)
                    .WithMessage(BusinessErrorMessage.PreferredEmployeeMembershipRequired)
                    .MustAsync(PreferredEmployeeIsEligibleAsync)
                    .WithMessage(BusinessErrorMessage.PreferredEmployeeNotEligible)
                    .WithName(nameof(Command.PreferredEmployeeId));
            });
        }

        /// <summary>
        /// The favourite-cleaner perk is Plus-only (owner ruling 2026-08-07, <c>Q-PLUS-03</c>;
        /// ADR-0039 D12.1 already gates the picker's availability flag on this same answer). The predicate
        /// is <c>UserMembershipRepository.EntitledForUserQuery</c> — the ONE ENTITLEMENT predicate, shared
        /// by all six Plus benefits — so <c>PastDue</c>, <c>Paused</c>, <c>Cancelled</c> and an elapsed
        /// period are all refused.
        ///
        /// <para>A trialing member is now refused too (owner ruling 2026-09-08, T-0690). That reverses the
        /// earlier position, under which a trial withheld only the METERED benefits (ADR-0035 AM-18) and
        /// this unmetered one was allowed. No Plus benefit is granted before payment, so there is no longer
        /// a metered/unmetered distinction to draw.</para>
        /// </summary>
        private async Task<bool> CallerHasActiveMembershipAsync(
            Command command,
            CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();

            return !string.IsNullOrEmpty(userId)
                && await _userMembershipRepository
                    .GetEntitledForUserNoTrackingAsync(userId, cancellationToken) is not null;
        }

        /// <summary>
        /// A completed order together, AND paid in the order's currency. A cleaner is paid in the
        /// currency of the country they work in; the board hides and take refuses a job in any other, so
        /// a hold granted across currencies could only lapse -- the push would be sent, the seat withheld
        /// for the whole hold, and the cleaner unable to act on it. One rule, one key: which term failed
        /// is not the customer's to learn.
        /// </summary>
        private async Task<bool> PreferredEmployeeIsEligibleAsync(
            Command command,
            Command _,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
            => await _orderRepository.UserHasCompletedOrderWithEmployeeAsync(
                   _userSessionProvider.GetUserId()!, command.PreferredEmployeeId!, cancellationToken)
               && (await _currencyResolutionService.ResolveCurrencyForEmployeeAsync(
                   command.PreferredEmployeeId!, cancellationToken)).Id
                  == await ResolveOrderCurrencyIdAsync(command, context, cancellationToken);

        private async Task<bool> HavePayCoverageAsync(
            Command command,
            IEnumerable<string> serviceIds,
            ValidationContext<Command> context,
            CancellationToken cancellationToken) =>
            await NamedCurrencyIsAnotherMarketsAsync(command, context, cancellationToken)
            || (await PayCoverageLookup.FindSelectionGapsAsync(
                _serviceRepository, _packageRepository, _payConfigRepository,
                serviceIds, [], await ResolveOrderCurrencyIdAsync(command, context, cancellationToken),
                cancellationToken)).Count == 0;

        private async Task<bool> HavePackagePayCoverageAsync(
            Command command,
            IEnumerable<string> packageIds,
            ValidationContext<Command> context,
            CancellationToken cancellationToken) =>
            await NamedCurrencyIsAnotherMarketsAsync(command, context, cancellationToken)
            || (await PayCoverageLookup.FindSelectionGapsAsync(
                _serviceRepository, _packageRepository, _payConfigRepository,
                [], packageIds, await ResolveOrderCurrencyIdAsync(command, context, cancellationToken),
                cancellationToken)).Count == 0;

        private async Task<bool> ArePricedInOrderCurrencyAsync(
            Command command,
            IEnumerable<string> serviceIds,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
        {
            if (await NamedCurrencyIsAnotherMarketsAsync(command, context, cancellationToken))
            {
                return true;
            }

            var ids = serviceIds.Distinct().ToList();
            var prices = await CataloguePriceLookup.ForServicesAsync(
                _servicePriceRepository, ids,
                await ResolveOrderCurrencyIdAsync(command, context, cancellationToken), cancellationToken);
            return ids.All(prices.ContainsKey);
        }

        private async Task<bool> ArePackagesPricedInOrderCurrencyAsync(
            Command command,
            IEnumerable<string> packageIds,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
        {
            if (await NamedCurrencyIsAnotherMarketsAsync(command, context, cancellationToken))
            {
                return true;
            }

            var ids = packageIds.Distinct().ToList();
            var prices = await CataloguePriceLookup.ForPackagesAsync(
                _packagePriceRepository, ids,
                await ResolveOrderCurrencyIdAsync(command, context, cancellationToken), cancellationToken);
            return ids.All(prices.ContainsKey);
        }

        private async Task<bool> NamedCurrencyIsAnotherMarketsAsync(
            Command command, ValidationContext<Command> context, CancellationToken cancellationToken)
            => !await CurrencyMatchesAddressCountryAsync(command, command, context, cancellationToken);

        private const string OrderCurrencyIdKey = "createOrder.orderCurrencyId";
        private const string OrderCountryIdKey = "createOrder.orderCountryId";

        /// <summary>
        /// A country the command does not determine is refused by the handler's address resolver with
        /// its own code; this rule only judges a resolved market. Both sides null (no market, no
        /// ambient tenant) is the design-time shape, not a production one.
        /// </summary>
        private async Task<bool> AddressCountryIsOperatedByAmbientTenantAsync(
            Command command,
            Command _,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
        {
            var countryId = await ResolveOrderCountryIdAsync(command, context, cancellationToken);
            if (countryId is null)
            {
                return true;
            }

            var resolution = await _operatorTenantResolver.ResolveAsync(countryId, cancellationToken);
            return resolution.OperatorTenantId == _tenantProvider.GetCurrentTenantId();
        }

        /// <summary>
        /// The service address's country, resolved ONCE per validation and cached beside the currency:
        /// the currency rule and the operator rule must judge the same country or the invariant they
        /// protect is only an argument.
        /// </summary>
        private async Task<string?> ResolveOrderCountryIdAsync(
            Command command, ValidationContext<Command> context, CancellationToken cancellationToken)
        {
            if (context.RootContextData.TryGetValue(OrderCountryIdKey, out var cached))
            {
                return cached as string;
            }

            var countryId = await _orderAddressResolver.ResolveCountryIdAsync(
                command, _userSessionProvider.GetUserId(), cancellationToken);
            context.RootContextData[OrderCountryIdKey] = countryId!;
            return countryId;
        }

        /// <summary>
        /// THE ORDER'S CURRENCY IS THE SERVICE ADDRESS'S COUNTRY'S (owner ruling 2026-09-12) -- the same
        /// resolution the handler stamps the order with, cached on the validation context because six
        /// rules ask for it and each resolution is two reads. A country the command does not determine
        /// (a missing or foreign saved row, no country in a multi-country platform, a country the
        /// platform does not service) resolves to the platform default here; the handler's address
        /// resolver refuses those with their own codes. A serviced country resolves to its currency or
        /// throws -- nothing is guessed for a named market.
        /// </summary>
        private async Task<string> ResolveOrderCurrencyIdAsync(
            Command command, ValidationContext<Command> context, CancellationToken cancellationToken)
        {
            if (context.RootContextData.TryGetValue(OrderCurrencyIdKey, out var cached) && cached is string id)
            {
                return id;
            }

            var countryId = await ResolveOrderCountryIdAsync(command, context, cancellationToken);
            var currency = await _currencyResolutionService.ResolveCurrencyForCountryAsync(
                countryId, cancellationToken);
            context.RootContextData[OrderCurrencyIdKey] = currency.Id;
            return currency.Id;
        }

        /// <summary>
        /// Null is the address country's currency by definition. A named one has to BE that currency:
        /// the market is a property of the booking, not a choice, so a client that was quoted in one
        /// currency and then moved the address into another market must re-quote rather than book the
        /// old price in the old currency.
        /// </summary>
        private async Task<bool> CurrencyMatchesAddressCountryAsync(
            Command command,
            Command _,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
            => string.IsNullOrEmpty(command.CurrencyId)
               || command.CurrencyId == await ResolveOrderCurrencyIdAsync(command, context, cancellationToken);

        /// <summary>
        /// The address country's currency must be one the platform can quote in -- switched on AND
        /// priced, the same predicate QuoteOrder applies -- so a country configured for a currency that
        /// is not yet operated is refused here rather than in the calculator.
        /// </summary>
        private async Task<bool> CurrencyIsOfferableAsync(
            Command command,
            Command _,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
            => await _currencyRepository.IsOfferableAsync(
                await ResolveOrderCurrencyIdAsync(command, context, cancellationToken), cancellationToken);

        private static bool OrderMustNotBeEmpty(Command command) => command.SelectedPackageIds.Any() ||
                                                                    command.SelectedServiceIds.Any();

        /// <summary>
        /// Mirrors the factory's span guard so an over-long selection is a business error rather than an
        /// exception.
        /// </summary>
        /// <remarks>
        /// <b>The double-count is INTENDED (owner ruling).</b> Selecting a package and a service inside it
        /// buys that service twice — performed twice, priced twice, twice as long. The sum is correct and
        /// <b>must not be "fixed" with a Distinct</b>. → /flows/booking-and-pricing
        /// </remarks>
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

        private const string PricingResultKey = "createOrder.pricingResult";

        /// <summary>
        /// Runs the ONE waiver-aware pricing recompute for this validation and classifies the outcome.
        /// Fails only when the submitted total is exactly the server's total minus the express surcharge
        /// — i.e. the client was quoted a waived price and the quota has since been exhausted, which is
        /// the single pricing input in this system that can change between two runs of a fixed command.
        /// Any other mismatch is left to <see cref="PriceMatchesAsync"/>, which reads the result this
        /// method cached rather than pricing the order a second time.
        /// </summary>
        private async Task<bool> ExpressWaiverStillAvailableAsync(
            Command command,
            Command _,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
        {
            // Pass CleaningDate so the calculator folds the express surcharge
            // in itself — replaces the legacy two-branch "either raw or
            // grossed-up" comparison we had before extras shipped.
            var selectedExtraSlugs = SelectedExtraSlugsFrom(command.Extras);
            var result = await _pricingCalculator.CalculateAsync(
                command.SelectedServiceIds,
                command.SelectedPackageIds,
                selectedExtraSlugs,
                command.Rooms,
                command.Bathrooms,
                // The address country's currency -- already offerable, because this chain stops on the
                // currency rules before it reaches here. A quote taken with the same country priced from
                // the same rows, so the price being compared was computed the same way.
                await ResolveOrderCurrencyIdAsync(command, context, cancellationToken),
                command.CleaningDate,
                _userSessionProvider.GetUserId(),
                DateTime.UtcNow,
                cancellationToken);

            context.RootContextData[PricingResultKey] = result;

            return result.TotalPrice == command.TotalPrice
                || !result.ExpressSurchargeApplied
                || command.TotalPrice != result.TotalPrice - result.ExpressSurchargeAmount;
        }

        private static Task<bool> PriceMatchesAsync(
            Command command,
            Command _,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
            => Task.FromResult(CachedPricing(context).TotalPrice == command.TotalPrice);

        private static OrderPricingResult CachedPricing(ValidationContext<Command> context)
            => (OrderPricingResult)context.RootContextData[PricingResultKey];

        // The promo rule cannot pick its message up front: which refusal applies is only known after
        // the preview inside the predicate. So the predicate hands the resolved message key to the rule
        // through the MessageFormatter, and the rule's template is nothing but this placeholder.
        private const string PromoErrorPlaceholder = "PromoError";
        private const string PromoErrorTemplate = "{" + PromoErrorPlaceholder + "}";

        /// <summary>
        /// A promo the server will not honour refuses the booking. The client displayed a discounted
        /// total the customer consented to; silently booking at full price is the same consent defect
        /// the express-waiver refusal exists to prevent. The preview is the one the handler's applier
        /// re-runs: the same code, the same pre-surcharge subtotal from the cached calculator result,
        /// and the address country's currency -- so a code bound to another market's currency, or one
        /// that expired or hit its cap between apply and submit, is refused here rather than dropped.
        /// No code is nothing to honour; a code with no signed-in customer is refused by the rule before
        /// this one, because the applier would silently drop it and book at full price.
        /// </summary>
        private bool PromoNamesASignedInCustomer(Command command)
            => string.IsNullOrEmpty(command.PromoCode)
               || !string.IsNullOrEmpty(_userSessionProvider.GetUserId());

        private async Task<bool> PromoWouldBeHonouredAsync(
            Command command,
            Command _,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(command.PromoCode) || string.IsNullOrEmpty(userId))
            {
                return true;
            }

            var pricing = CachedPricing(context);
            var preview = await _promoCodeService.PreviewAsync(
                command.PromoCode,
                userId,
                pricing.TotalPrice - pricing.ExpressSurchargeAmount,
                await ResolveOrderCurrencyIdAsync(command, context, cancellationToken),
                cancellationToken);
            if (preview.Error is not { } error)
            {
                return true;
            }

            context.MessageFormatter.AppendArgument(PromoErrorPlaceholder, PromoErrorMessage(error));
            return false;
        }

        private static string PromoErrorMessage(PromoCodeError error) => error switch
        {
            PromoCodeError.NotFound => BusinessErrorMessage.PromoNotFound,
            PromoCodeError.Inactive => BusinessErrorMessage.PromoInactive,
            PromoCodeError.Expired => BusinessErrorMessage.PromoExpired,
            PromoCodeError.NotYetValid => BusinessErrorMessage.PromoNotYetValid,
            PromoCodeError.GlobalLimitReached => BusinessErrorMessage.PromoGlobalLimitReached,
            PromoCodeError.PerUserLimitReached => BusinessErrorMessage.PromoPerUserLimitReached,
            PromoCodeError.BelowMinimumOrderAmount => BusinessErrorMessage.PromoBelowMinimumOrderAmount,
            PromoCodeError.CurrencyMismatch => BusinessErrorMessage.PromoCurrencyMismatch,
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, null),
        };

        /// <summary>
        /// Filter the slug-keyed Extras map down to slugs the client
        /// actually selected (value=true). Centralised so the validator,
        /// handler, and pricing calculator stay in lockstep with how the
        /// field is interpreted.
        /// </summary>
        internal static IEnumerable<string> SelectedExtraSlugsFrom(Dictionary<string, bool>? extras)
            => extras == null
                ? Array.Empty<string>()
                : extras.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
    }

    public record Command(
        string CustomerName,
        string CustomerEmail,
        string CustomerPhone,
        AddressDto? CustomerAddress,
        string? SavedAddressId,
        IEnumerable<string> SelectedPackageIds,
        IEnumerable<string> SelectedServiceIds,
        int Rooms,
        int Bathrooms,
        Dictionary<string, bool> Extras,
        DateTime CleaningDate,
        PaymentType PaymentType,
        string? CurrencyId,
        decimal TotalPrice,
        string Language = Constants.Language.English,
        string? PromoCode = null,
        string? ReferralCode = null,
        string? PreferredEmployeeId = null,
        /// <summary>
        /// Optional free-text note from the customer ("gate code 1234", "dog is
        /// friendly"). Persisted on the Order and rendered read-only by the
        /// partner + admin surfaces. Optional on purpose: clients built before
        /// this field existed simply omit it and behave exactly as before.
        /// </summary>
        string? SpecialInstructions = null,
        /// <summary>
        /// Optional free-text entry instructions ("key under the mat", "gate
        /// code 4455"). Persisted on the Order and rendered read-only by the
        /// partner + admin surfaces, unconditionally — it carries no extra
        /// access control today despite being the more sensitive of the two
        /// note fields. Optional on purpose: clients built before this field
        /// existed simply omit it and behave exactly as before.
        /// </summary>
        string? AccessInstructions = null,
        /// <summary>
        /// Which floor, and which door on it. Both optional and both null for a
        /// house, which has neither. They are carried on the order rather than
        /// on the address because addresses are deduped across users at the same
        /// street — see <c>Order.CustomerFloor</c>.
        /// </summary>
        string? CustomerFloor = null,
        string? CustomerApartment = null,
        string? AccessMode = null,
        // The terms tick as the client asserted it. Null is a client that sends nothing; it is recorded
        // as "not asserted", never refused (ADR-0062 D4).
        bool? TermsAccepted = null) : ICommand<Response>, IOperatorScopedRequest
    {
        // A guest's market is the inline address's country; a guest cannot name a saved address, and a
        // request with no country lands in the default market (ADR-0061 D3). The validator's operator
        // rule is what makes the resolver's country agree with this one.
        string? IOperatorScopedRequest.CountryId => CustomerAddress?.CountryId;
    }

    public record Response(
        string Id,
        string ConfirmationCode,
        string? StripeSessionId);

    /// <summary>
    /// The booking as the server priced and stored it (ADR-0062 D3): every figure is read off the
    /// persisted order and the calculator's answer, never off the request — except the terms tick,
    /// which only the client can assert. No contact detail, no address text, no instructions, and no
    /// preferred cleaner: the erasure nulls that on purpose and the row must not undo it.
    /// </summary>
    public record OrderBookingEvidence(
        string OrderId,
        decimal TotalPrice,
        decimal NetPrice,
        decimal VatAmount,
        decimal? AppliedVatRate,
        string CurrencyCode,
        string CountryId,
        LoyaltyTier? TierAtPurchase,
        decimal? TierDiscountAmount,
        string? PromoCodeId,
        decimal? PromoDiscountAmount,
        string? MembershipPlanIdAtPurchase,
        decimal? MembershipDiscountAmount,
        decimal ExpressSurchargeAmount,
        bool ExpressWaivedByMembership,
        decimal CreditAppliedAmount,
        PaymentType PaymentType,
        DateTimeOffset CleaningDateTime,
        decimal LeadTimeHours,
        IReadOnlyList<string> PackageIds,
        IReadOnlyList<string> ServiceIds,
        IReadOnlyList<string> ExtraSlugs,
        int Rooms,
        int Bathrooms,
        string? SavedAddressId,
        string AddressId,
        string? RecurringTemplateId,
        string Language,
        bool IsGuest,
        CancellationPolicyShown CancellationPolicyShown,
        bool? TermsAccepted,
        string TermsVersionAccepted) : ICustomerAuditPayload
    {
        public static OrderBookingEvidence From(
            Order order,
            Command command,
            OrderPricingResult pricing,
            Currency currency,
            Address address,
            bool expressWaiverReserved,
            CancellationPolicy cancellationPolicy,
            DateTime nowUtc) => new(
            OrderId: order.Id,
            TotalPrice: order.TotalPrice,
            NetPrice: order.NetAmount,
            VatAmount: order.VatAmount,
            AppliedVatRate: order.AppliedVatRate,
            CurrencyCode: currency.Code,
            CountryId: address.CountryId,
            TierAtPurchase: order.TierAtPurchase,
            TierDiscountAmount: order.TierDiscountAmount,
            PromoCodeId: order.PromoCodeId,
            PromoDiscountAmount: order.PromoDiscountAmount,
            MembershipPlanIdAtPurchase: order.MembershipPlanIdAtPurchase,
            MembershipDiscountAmount: order.MembershipDiscountAmount,
            ExpressSurchargeAmount: pricing.ExpressSurchargeAmount,
            ExpressWaivedByMembership: expressWaiverReserved,
            CreditAppliedAmount: order.CreditAppliedAmount,
            PaymentType: order.PaymentType,
            CleaningDateTime: new DateTimeOffset(DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc)),
            LeadTimeHours: Math.Round((decimal)(order.CleaningDateTime - nowUtc).TotalHours, 2),
            PackageIds: command.SelectedPackageIds.ToList(),
            ServiceIds: command.SelectedServiceIds.ToList(),
            ExtraSlugs: order.SelectedExtras.Select(e => e.Slug).ToList(),
            Rooms: order.Rooms,
            Bathrooms: order.Bathrooms,
            SavedAddressId: command.SavedAddressId,
            AddressId: address.Id,
            RecurringTemplateId: order.RecurringTemplateId,
            Language: command.Language,
            IsGuest: string.IsNullOrEmpty(order.UserId),
            CancellationPolicyShown: CancellationPolicyShown.From(cancellationPolicy),
            TermsAccepted: command.TermsAccepted,
            TermsVersionAccepted: LegalDocumentVersions.CustomerTerms);
    }

    /// <summary>
    /// The cancellation schedule the booking was made under: the platform figures, plus the free window
    /// this customer actually had (a Plus window is narrower than the standard 24 h).
    /// </summary>
    public record CancellationPolicyShown(
        int FreeHours,
        int PartialHours,
        decimal PartialRate,
        decimal LastMinuteRate,
        int FreeHoursForThisCustomer)
    {
        public static CancellationPolicyShown From(CancellationPolicy policy) => new(
            BookingPolicy.FreeCancellationHours,
            BookingPolicy.PartialCancellationHours,
            BookingPolicy.PartialCancellationFeeRate,
            BookingPolicy.LastMinuteCancellationFeeRate,
            policy.FreeCancellationHours);
    }

    public class Handler(
        ICurrencyResolutionService currencyResolutionService,
        IUserSessionProvider userSessionProvider,
        IOrderPricingCalculator pricingCalculator,
        IOrderFactory orderFactory,
        IOrderAddressResolver orderAddressResolver,
        IOrderPromoApplier orderPromoApplier,
        IOrderLateReferralAcceptor orderLateReferralAcceptor,
        IOrderPaymentDispatcher orderPaymentDispatcher,
        IExpressWaiverConsumer expressWaiverConsumer,
        ICreditAccountRepository creditAccountRepository,
        ICancellationPolicyResolver cancellationPolicyResolver,
        IAuditContext auditContext,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId() ?? string.Empty;

            // ONE clock reading, threaded through the resolver, the reservation and every express-window
            // evaluation below. Reading DateTime.UtcNow again further down would let the resolver see "in
            // window" and the factory see "out of window", producing a live slot attached to an order
            // whose price carries no surcharge — the one loss here with no release rule and no sweep.
            var nowUtc = DateTime.UtcNow;

            await orderLateReferralAcceptor.AcceptIfPresentAsync(
                command.ReferralCode, userId, cancellationToken);

            var addressResult = await orderAddressResolver.ResolveAsync(command, userId, cancellationToken);
            if (addressResult.Failure is { } failure)
            {
                return BusinessResult.Failure<Response>(failure);
            }
            var address = addressResult.Address!;

            // THE ORDER IS STAMPED WITH THE SERVICE ADDRESS'S COUNTRY'S CURRENCY (owner ruling
            // 2026-09-12): the market is a property of the booking, and the validator has already
            // refused a named CurrencyId that is not this one and a resolved one that is not offerable.
            // The quote resolved the same way from the same country, so the two agree by construction.
            var currency = await currencyResolutionService.ResolveCurrencyForCountryAsync(
                address.CountryId, cancellationToken);

            // The calculator now surfaces the broken-out (raw + extras +
            // surcharge) shape, so OrderFactory can take a raw-pre-surcharge
            // subtotal and re-apply the surcharge after the discount. Quote
            // gross is `command.TotalPrice` (price client agreed to); raw
            // subtotal is total minus the surcharge portion the calculator
            // computed for this CleaningDate.
            var selectedExtraSlugs = Validator.SelectedExtraSlugsFrom(command.Extras).ToList();
            var calc = await pricingCalculator.CalculateAsync(
                command.SelectedServiceIds,
                command.SelectedPackageIds,
                selectedExtraSlugs,
                command.Rooms,
                command.Bathrooms,
                currency.Id,
                command.CleaningDate,
                userId,
                nowUtc,
                cancellationToken);
            var rawSubtotal = calc.TotalPrice - calc.ExpressSurchargeAmount;

            // Mode A — the slot is claimed BEFORE the waived price is frozen, never after. An express
            // waiver requires nothing but an active subscription, so a soft cap is farmable by every
            // subscriber with concurrent requests alone; the promo path's reserve-after-persist ordering
            // is safe there only because a promo needs a code an operator issued.
            //
            // The trigger is the CALCULATOR's answer, not a second resolve's. rawSubtotal above was
            // derived from the calculator's price, so the factory's waiver decision must equal the
            // calculator's or the price it freezes is not the one anybody agreed to — and the two can
            // genuinely differ, in both directions, if a concurrent booking or release moves the quota
            // between the two reads.
            MembershipBenefitUsage? reservation = null;
            if (calc.ExpressSurchargeWaivedByMembership)
            {
                var waiver = await expressWaiverConsumer.ResolveAsync(
                    userId, command.CleaningDate, nowUtc, cancellationToken);
                reservation = await expressWaiverConsumer.TryReserveAsync(
                    waiver, nowUtc, cancellationToken);

                if (reservation == null)
                {
                    // The validator approved a WAIVED total and the slot is gone. Pricing the order at
                    // waived + 20% here would charge 20% more than the customer consented to, with no
                    // error and no field in the three-field response to notice it by; honoring the waived
                    // price without a committed slot is the soft cap this design rejects. Re-quote.
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.TotalPrice),
                        BusinessErrorMessage.ExpressWaiverNoLongerAvailable));
                }
            }

            // Promo preview lives outside the factory because it's a one-off
            // input (not a stored snapshot like tier/membership) and needs to
            // be Apply()d after the order persists, not just previewed.
            var promo = await orderPromoApplier.PreviewAsync(
                command, userId, rawSubtotal, currency.Id, cancellationToken);

            var order = await orderFactory.CreateAsync(new CreateOrderInput(
                UserId: userId,
                CustomerName: command.CustomerName,
                CustomerEmail: command.CustomerEmail,
                CustomerPhone: command.CustomerPhone,
                Address: address,
                Rooms: command.Rooms,
                Bathrooms: command.Bathrooms,
                SelectedExtraSlugs: selectedExtraSlugs,
                CleaningDate: command.CleaningDate,
                PaymentType: command.PaymentType,
                Currency: currency,
                SelectedServiceIds: command.SelectedServiceIds,
                SelectedPackageIds: command.SelectedPackageIds,
                RawSubtotal: rawSubtotal,
                NowUtc: nowUtc,
                ReservedExpressWaiver: reservation,
                PromoDiscountAmount: promo.DiscountAmount,
                PromoCodeId: promo.PromoCodeId,
                PreferredEmployeeId: command.PreferredEmployeeId,
                RecurringTemplateId: null,
                SpecialInstructions: command.SpecialInstructions,
                AccessInstructions: command.AccessInstructions,
                CustomerFloor: command.CustomerFloor,
                CustomerApartment: command.CustomerApartment,
                AccessMode: command.AccessMode), cancellationToken);

            if (reservation != null)
            {
                // A change-tracked update, so EF's dependency-ordered batch puts the Orders INSERT ahead
                // of it inside the one SaveChangesAsync. Out-of-band here would fire against an Orders
                // row that does not exist until the pipeline commits, after this handler returns — 23503,
                // on a booking whose Stripe session has already been minted.
                await expressWaiverConsumer.AttachOrderAsync(reservation, order.Id, cancellationToken);
            }

            // TAKE THE CREDIT FIRST, then price the order from what was actually taken. Owner ruling
            // 2026-09-05: credit applies automatically, to the next eligible order - there is no
            // customer-facing "spend it now" control to consult.
            //
            // The order matters, and an earlier draft had it the other way round. Applying first and
            // debiting after left the conditional UPDATE with nothing to arbitrate: two checkouts by
            // the same customer in two tabs both read the same balance, both discounted their order by
            // it, and only the first debit landed - the second order kept its discount for free. Two
            // browser tabs is not an exotic race, and the loss is the whole balance, repeatable.
            //
            // Debiting first inverts the exposure: the balance is arbitrated by the database, and what
            // is left is a failed dispatch, which the compensating return below covers.
            var intendedCredit = await TakeCreditForOrderAsync(order, userId, cancellationToken);
            if (intendedCredit > 0m)
            {
                order.ApplyCredit(intendedCredit, userId);
            }

            var dispatch = await orderPaymentDispatcher.DispatchAsync(
                order, command.Language, cancellationToken);
            if (dispatch.Failure is { } dispatchFailure)
            {
                // Stripe is unreachable and this order will not exist - the pipeline commits nothing on
                // a failure. Put the credit back before returning: TryReturnAsync is its own statement
                // precisely so it survives a request that is about to roll back, and it is keyed on the
                // order id so a retry cannot double-return.
                await creditAccountRepository.ReturnCreditAsync(
                    order, intendedCredit, $"dispatch-failed:{order.Id}", userId, cancellationToken);

                return BusinessResult.Failure<Response>(dispatchFailure);
            }

            // Promo persistence runs after the order is in the repo so the
            // promo row gets the order id. Failure logs but doesn't roll back —
            // the customer already paid and the promo just doesn't get tracked.
            await orderPromoApplier.ApplyAsync(
                command, userId, order, rawSubtotal, currency.Id, cancellationToken);

            var cancellationPolicy = await cancellationPolicyResolver.ResolveForUserAsync(
                order.UserId, cancellationToken);
            auditContext.RecordEvidence("Order", order.Id, OrderBookingEvidence.From(
                order, command, calc, currency, address, reservation != null, cancellationPolicy, nowUtc));

            return BusinessResult.Success(new Response(
                Id: order.Id,
                ConfirmationCode: order.ConfirmationCode,
                // The wire contract's name, kept: it has always carried the Checkout URL the
                // browser is redirected to, and the web client reads it as one.
                StripeSessionId: dispatch.CheckoutUrl));
        }

        /// <summary>
        /// Take whatever credit this order may use, and answer with the amount actually taken.
        ///
        /// <para>Resolve and debit are ONE step because splitting them is what created the race: the
        /// read takes no lock, so the only figure that can be trusted is the one the conditional UPDATE
        /// actually removed. A false answer - the balance moved between the read and the write - is not
        /// an error, it is zero: this order simply pays full price.</para>
        ///
        /// <para>Three gates decide whether any credit is eligible at all, each for its own reason.
        /// <b>Card only</b> - a cash order is settled to the cleaner's hand on the doorstep, and there
        /// is no mechanism for them to collect a different figure than the one on the job sheet.
        /// <b>Same currency</b> - a balance is held in one currency and the platform will not convert
        /// it silently at spend time. <b>Capped</b> - owner ruling 2026-09-05, the card always pays a
        /// share. -&gt; BookingPolicy.CapCreditForOrder</para>
        /// </summary>
        private async Task<decimal> TakeCreditForOrderAsync(
            Order order, string userId, CancellationToken cancellationToken)
        {
            if (order.PaymentType != PaymentType.Card || string.IsNullOrEmpty(userId))
            {
                return 0m;
            }

            // Asked FOR the order's currency rather than asked-then-compared. The comparison below is
            // kept as a belt-and-braces assertion on a money path, but it can no longer be the thing
            // that decides: an unkeyed read returned whichever account existed, so a customer with a
            // matching balance and a second account could be told they had none.
            var spendable = await creditAccountRepository.GetSpendableAsync(
                userId, order.CurrencyId, cancellationToken);
            if (spendable == null || spendable.CurrencyId != order.CurrencyId)
            {
                return 0m;
            }

            var eligible = BookingPolicy.CapCreditForOrder(spendable.Balance, order.TotalPrice);
            if (eligible <= 0m)
            {
                return 0m;
            }

            // The order id IS the idempotency key, so this order can never be debited twice however
            // many times any future caller re-runs this step. A retried REQUEST mints a new order with
            // a new id and is a new debit - which is correct: it is a different booking.
            var taken = await creditAccountRepository.TryDebitAsync(
                creditAccountId: spendable.AccountId,
                amount: eligible,
                reason: CreditTransactionReason.OrderPayment,
                idempotencyKey: $"order-payment-{order.Id}",
                actorId: userId,
                cancellationToken: cancellationToken,
                orderId: order.Id);

            if (!taken)
            {
                // A concurrent checkout drained the balance between the read and the write. Nothing is
                // wrong and nothing is lost - this order is priced at full price, which is what the
                // customer would have seen had they started it a second later.
                logger.LogInformation(
                    "Credit debit of {Amount} was refused for order {OrderId}; the balance moved between "
                    + "the read and the write, so the order is priced at full price.",
                    eligible, order.Id);
                return 0m;
            }

            return eligible;
        }
    }
}
