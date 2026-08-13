using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

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

        public Validator(
            IPackageRepository packageRepository,
            IServiceRepository serviceRepository,
            ICurrencyRepository currencyRepository,
            IOrderPricingCalculator pricingCalculator,
            IOrderRepository orderRepository,
            IUserMembershipRepository userMembershipRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeePayConfigRepository payConfigRepository)
        {
            _packageRepository = packageRepository;
            _serviceRepository = serviceRepository;
            _payConfigRepository = payConfigRepository;
            _pricingCalculator = pricingCalculator;
            _orderRepository = orderRepository;
            _userMembershipRepository = userMembershipRepository;
            _userSessionProvider = userSessionProvider;

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

            When(x => !string.IsNullOrEmpty(x.CurrencyId), () =>
            {
                RuleFor(x => x.CurrencyId!)
                    .MustAsync(currencyRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.InvalidCurrency);
            });

            RuleFor(x => x)
                .Must(cmd => (cmd.CustomerAddress != null) ^ (!string.IsNullOrEmpty(cmd.SavedAddressId)))
                .WithMessage(BusinessErrorMessage.OrderAddressExactlyOneRequired)
                .WithName(nameof(Command.CustomerAddress));

            // The pay-coverage terms mirror OrderFactory's backstop so the customer gets a 400 instead
            // of a 500. They reuse the existing selection codes deliberately: the booking wizard never
            // offers an entry without a platform-wide pay config, so a caller that reaches this is
            // submitting an id it was not shown, and a dedicated customer-visible key would describe an
            // internal payroll condition to the wrong audience.
            RuleFor(x => x.SelectedServiceIds)
                .Cascade(CascadeMode.Stop)
                .MustAsync(serviceRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices)
                .MustAsync(HavePayCoverageAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices);

            RuleFor(x => x.SelectedPackageIds)
                .Cascade(CascadeMode.Stop)
                .MustAsync(packageRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage)
                .MustAsync(HavePackagePayCoverageAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage);

            // The two price rules are ORDERED and share one calculator run. The waiver rule goes first
            // because Cascade.Stop means only the first failure is reported, and a member who lost their
            // free upgrade between the quote and here must get the dedicated code — TotalPriceNotMatch is
            // rendered by every client as a generic "the price changed", which is exactly the sentence
            // that cannot explain this.
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(OrderMustNotBeEmpty)
                .WithMessage(BusinessErrorMessage.EmptyOrder)
                .MustAsync(SpanWithinCapAsync)
                .WithMessage(BusinessErrorMessage.OrderSpanExceedsMaximum)
                .MustAsync(ExpressWaiverStillAvailableAsync)
                .WithMessage(BusinessErrorMessage.ExpressWaiverNoLongerAvailable)
                .MustAsync(PriceMatchesAsync)
                .WithMessage(BusinessErrorMessage.TotalPriceNotMatch);

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
        /// is <c>UserMembershipRepository.ActiveForUserQuery</c> — the ONE live-membership predicate the
        /// platform has, never a second one — so <c>PastDue</c>, <c>Paused</c>, <c>Cancelled</c> and an
        /// elapsed period are all refused, and a trialing member is allowed: trial withholds only the
        /// METERED benefits (ADR-0035 AM-18), and this one is not metered.
        /// </summary>
        private async Task<bool> CallerHasActiveMembershipAsync(
            Command command,
            CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();

            return !string.IsNullOrEmpty(userId)
                && await _userMembershipRepository
                    .GetActiveForUserNoTrackingAsync(userId, cancellationToken) is not null;
        }

        private async Task<bool> PreferredEmployeeIsEligibleAsync(
            Command command,
            CancellationToken cancellationToken)
            => await _orderRepository.UserHasCompletedOrderWithEmployeeAsync(
                _userSessionProvider.GetUserId()!, command.PreferredEmployeeId!, cancellationToken);

        private async Task<bool> HavePayCoverageAsync(
            IEnumerable<string> serviceIds, CancellationToken cancellationToken) =>
            (await PayCoverageLookup.FindSelectionGapsAsync(
                _serviceRepository, _packageRepository, _payConfigRepository,
                serviceIds, [], cancellationToken)).Count == 0;

        private async Task<bool> HavePackagePayCoverageAsync(
            IEnumerable<string> packageIds, CancellationToken cancellationToken) =>
            (await PayCoverageLookup.FindSelectionGapsAsync(
                _serviceRepository, _packageRepository, _payConfigRepository,
                [], packageIds, cancellationToken)).Count == 0;

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

        private const string PriceMatchesKey = "createOrder.priceMatches";

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
                command.CurrencyId,
                command.CleaningDate,
                _userSessionProvider.GetUserId(),
                DateTime.UtcNow,
                cancellationToken);

            var priceMatches = result.TotalPrice == command.TotalPrice;
            context.RootContextData[PriceMatchesKey] = priceMatches;

            return priceMatches
                || !result.ExpressSurchargeApplied
                || command.TotalPrice != result.TotalPrice - result.ExpressSurchargeAmount;
        }

        private static Task<bool> PriceMatchesAsync(
            Command command,
            Command _,
            ValidationContext<Command> context,
            CancellationToken cancellationToken)
            => Task.FromResult(
                context.RootContextData.TryGetValue(PriceMatchesKey, out var matches) && matches is true);

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
        string? AccessInstructions = null) : ICommand<Response>;

    public record Response(
        string Id,
        string ConfirmationCode,
        string? StripeSessionId);

    public class Handler(
        ICurrencyRepository currencyRepository,
        IUserSessionProvider userSessionProvider,
        IOrderPricingCalculator pricingCalculator,
        IOrderFactory orderFactory,
        IOrderAddressResolver orderAddressResolver,
        IOrderPromoApplier orderPromoApplier,
        IOrderLateReferralAcceptor orderLateReferralAcceptor,
        IOrderPaymentDispatcher orderPaymentDispatcher,
        IExpressWaiverConsumer expressWaiverConsumer) : ICommandHandler<Command, Response>
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

            var currency = string.IsNullOrEmpty(command.CurrencyId)
                ? await currencyRepository.GetDefaultAsync(cancellationToken)
                : await currencyRepository.GetByIdAsync(command.CurrencyId, cancellationToken);

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
                command.CurrencyId,
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
                command, userId, rawSubtotal, currency!.Id, cancellationToken);

            var order = await orderFactory.CreateAsync(new CreateOrderInput(
                UserId: userId,
                CustomerName: command.CustomerName,
                CustomerEmail: command.CustomerEmail,
                CustomerPhone: command.CustomerPhone,
                Address: address,
                Rooms: command.Rooms,
                Bathrooms: command.Bathrooms,
                Extras: command.Extras,
                CleaningDate: command.CleaningDate,
                PaymentType: command.PaymentType,
                Currency: currency!,
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
                AccessInstructions: command.AccessInstructions), cancellationToken);

            if (reservation != null)
            {
                // A change-tracked update, so EF's dependency-ordered batch puts the Orders INSERT ahead
                // of it inside the one SaveChangesAsync. Out-of-band here would fire against an Orders
                // row that does not exist until the pipeline commits, after this handler returns — 23503,
                // on a booking whose Stripe session has already been minted.
                await expressWaiverConsumer.AttachOrderAsync(reservation, order.Id, cancellationToken);
            }

            var dispatch = await orderPaymentDispatcher.DispatchAsync(
                order, command.Language, cancellationToken);
            if (dispatch.Failure is { } dispatchFailure)
            {
                return BusinessResult.Failure<Response>(dispatchFailure);
            }

            // Promo persistence runs after the order is in the repo so the
            // promo row gets the order id. Failure logs but doesn't roll back —
            // the customer already paid and the promo just doesn't get tracked.
            await orderPromoApplier.ApplyAsync(
                command, userId, order, rawSubtotal, currency!.Id, cancellationToken);

            return BusinessResult.Success(new Response(
                Id: order.Id,
                ConfirmationCode: order.ConfirmationCode,
                StripeSessionId: dispatch.StripeSessionId));
        }
    }
}
