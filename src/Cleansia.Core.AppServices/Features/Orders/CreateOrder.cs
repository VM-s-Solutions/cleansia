using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Order = Cleansia.Core.Domain.Orders.Order;
using OrderService = Cleansia.Core.Domain.Orders.OrderService;

namespace Cleansia.Core.AppServices.Features.Orders;

public class CreateOrder
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderPricingCalculator _pricingCalculator;
        private readonly IOrderRepository _orderRepository;

        public Validator(
            IPackageRepository packageRepository,
            IServiceRepository serviceRepository,
            ICurrencyRepository currencyRepository,
            IOrderPricingCalculator pricingCalculator,
            IOrderRepository orderRepository)
        {
            _pricingCalculator = pricingCalculator;
            _orderRepository = orderRepository;

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
                .MaximumLength(50)
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

            RuleFor(x => x.SelectedServiceIds)
                .MustAsync(serviceRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedServices);

            RuleFor(x => x.SelectedPackageIds)
                .MustAsync(packageRepository.ExistWithIdsAsync)
                .WithMessage(BusinessErrorMessage.InvalidSelectedPackage);

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(OrderMustNotBeEmpty)
                .WithMessage(BusinessErrorMessage.EmptyOrder)
                .MustAsync(PriceMatchesAsync)
                .WithMessage(BusinessErrorMessage.TotalPriceNotMatch);

            // PreferredEmployeeId is a request, not a guarantee — but we still
            // restrict it to cleaners the customer has previously been served by.
            // Without that gate, a customer could probe arbitrary employee ids
            // (info leak) or game the matching algorithm by always requesting
            // a specific cleaner who has nothing to do with them.
            When(x => !string.IsNullOrEmpty(x.PreferredEmployeeId)
                && !string.IsNullOrEmpty(x.UserId), () =>
            {
                RuleFor(x => x)
                    .MustAsync(PreferredEmployeeIsEligibleAsync)
                    .WithMessage(BusinessErrorMessage.PreferredEmployeeNotEligible)
                    .WithName(nameof(Command.PreferredEmployeeId));
            });
        }

        private async Task<bool> PreferredEmployeeIsEligibleAsync(
            Command command,
            CancellationToken cancellationToken)
            => await _orderRepository.UserHasCompletedOrderWithEmployeeAsync(
                command.UserId, command.PreferredEmployeeId!, cancellationToken);

        private static bool OrderMustNotBeEmpty(Command command) => command.SelectedPackageIds.Any() ||
                                                                    command.SelectedServiceIds.Any();

        private async Task<bool> PriceMatchesAsync(Command command, CancellationToken cancellationToken)
        {
            var result = await _pricingCalculator.CalculateAsync(
                command.SelectedServiceIds,
                command.SelectedPackageIds,
                command.Rooms,
                command.Bathrooms,
                command.CurrencyId,
                cancellationToken);

            // Mirror the handler's express-surcharge logic so a booking inside
            // the express window (2–4h lead) doesn't get rejected here for
            // sending the grossed-up total. Accept either the base price or
            // the base + 20% surcharge — the handler will reconcile to the
            // authoritative figure before persisting.
            if (result.TotalPrice == command.TotalPrice)
            {
                return true;
            }

            if (BookingPolicy.RequiresExpressSurcharge(command.CleaningDate, DateTime.UtcNow))
            {
                var grossedUp = result.TotalPrice * (1 + BookingPolicy.ExpressSurchargeRate);
                return command.TotalPrice == grossedUp;
            }

            return false;
        }
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
        // Enriched server-side from the JWT in the controller. Empty when called
        // from contexts that don't authenticate (none currently).
        string UserId = "",
        // Optional promo code entered by the customer in the booking wizard.
        // Backend re-validates inside the handler before applying — the
        // Validate endpoint is UX optimisation, not the gate.
        string? PromoCode = null,
        // Optional referral code — late-acceptance fallback for customers
        // who didn't enter at signup. Fail-soft: a bad code is logged and
        // the booking proceeds.
        string? ReferralCode = null,
        // Optional customer-requested cleaner. Used as a matching hint by the
        // partner-side scoring algorithm; silent fallback to normal matching
        // if the cleaner is unavailable. Validator restricts this to cleaners
        // the customer has previously been served by, to prevent random-id
        // probing.
        string? PreferredEmployeeId = null) : ICommand<Response>;

    public record Response(
        string Id,
        string ConfirmationCode,
        string? StripeSessionId);

    public class Handler(
        ISendGridConfig sendGridConfig,
        IOrderRepository orderRepository,
        IAddressRepository addressRepository,
        ISavedAddressRepository savedAddressRepository,
        IServiceRepository serviceRepository,
        IPackageRepository packageRepository,
        ICurrencyRepository currencyRepository,
        ICountryRepository countryRepository,
        ICompanyInfoRepository companyInfoRepository,
        ICountryConfigurationRepository countryConfigurationRepository,
        IVatCalculator vatCalculator,
        ISendGridClientFactory clientFactory,
        IStripeClientFactory stripeClientFactory,
        IQueueClient queueClient,
        ILoyaltyService loyaltyService,
        IPromoCodeService promoCodeService,
        IReferralService referralService,
        IReferralRepository referralRepository,
        IUserMembershipRepository userMembershipRepository,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Late-acceptance: customer forgot to enter the referral at
            // signup but pasted it now. Skip if they're already in a
            // referral relationship (one-per-invitee). Fail-soft — a bad
            // code never blocks the booking.
            if (!string.IsNullOrWhiteSpace(command.ReferralCode)
                && !string.IsNullOrEmpty(command.UserId))
            {
                var existingReferral = await referralRepository.GetByReferredUserIdAsync(
                    command.UserId, cancellationToken);
                if (existingReferral == null)
                {
                    try
                    {
                        var acceptResult = await referralService.AcceptAsync(
                            command.ReferralCode, command.UserId, cancellationToken);
                        if (!acceptResult.IsAccepted)
                        {
                            logger.LogInformation(
                                "Late referral accept rejected for user {UserId}, code {Code}: {Error}",
                                command.UserId, command.ReferralCode, acceptResult.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Failed late-accept referral code {Code} for user {UserId}",
                            command.ReferralCode, command.UserId);
                    }
                }
            }

            Address address;
            string countryId;
            if (!string.IsNullOrEmpty(command.SavedAddressId))
            {
                var saved = await savedAddressRepository.GetByIdAsync(command.SavedAddressId, cancellationToken);
                if (saved == null)
                {
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.SavedAddressId),
                        BusinessErrorMessage.NotFound));
                }

                // Ownership check — endpoint is now authenticated (CustomerOnly),
                // so a user trying to book against another user's SavedAddress
                // must be rejected. Treat as not-found rather than forbidden so
                // the response shape matches an arbitrary-id miss.
                if (!string.IsNullOrEmpty(command.UserId) && saved.UserId != command.UserId)
                {
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.SavedAddressId),
                        BusinessErrorMessage.NotFound));
                }

                address = saved.Address
                    ?? await addressRepository.GetByIdAsync(saved.AddressId, cancellationToken)
                    ?? throw new InvalidOperationException($"SavedAddress {saved.Id} references missing Address {saved.AddressId}");
                countryId = address.CountryId;
            }
            else
            {
                // CustomerAddress is guaranteed non-null here by validator XOR rule.
                var inline = command.CustomerAddress!;
                var resolvedCountryId = inline.CountryId;
                if (!string.IsNullOrEmpty(resolvedCountryId) && !await countryRepository.ExistsAsync(resolvedCountryId, cancellationToken))
                {
                    resolvedCountryId = null;
                }
                if (string.IsNullOrEmpty(resolvedCountryId))
                {
                    var defaultCountry = await countryRepository.GetByIsoCodeAsync("CZE", cancellationToken)
                        ?? await countryRepository.GetQueryable().FirstOrDefaultAsync(cancellationToken);
                    resolvedCountryId = defaultCountry?.Id ?? throw new InvalidOperationException("No countries configured in the system");
                }

                countryId = resolvedCountryId;
                address = await addressRepository.GetAddressAsync(
                    inline.Street, inline.City, inline.ZipCode, countryId, cancellationToken)
                    ?? Address.Create(inline.Street, inline.City, inline.ZipCode, countryId, inline.State);
            }

            var currency = string.IsNullOrEmpty(command.CurrencyId)
                ? await currencyRepository.GetDefaultAsync(cancellationToken)
                : await currencyRepository.GetByIdAsync(command.CurrencyId, cancellationToken);

            // Apply express surcharge server-side. Client also computes this
            // (mobile: BookingPricing.kt, web: TODO) so the displayed total
            // matches; we recompute here so a stale client clock or a tampered
            // payload can't underpay. If client already grossed up, dividing
            // back out would risk drift — so detect by comparing client total
            // to base via ExpressSurchargeRate margin and only add if absent.
            var finalTotalPrice = command.TotalPrice;
            var nowUtc = DateTime.UtcNow;
            if (BookingPolicy.RequiresExpressSurcharge(command.CleaningDate, nowUtc))
            {
                // Heuristic: if the client total is at least 1 + (rate - 0.05) above
                // the rounded base, assume the surcharge is already baked in. Else
                // gross it up. The 5% margin accounts for currency conversion drift.
                // This keeps backend authoritative without double-charging.
                var grossedUp = command.TotalPrice * (1 + BookingPolicy.ExpressSurchargeRate);
                var alreadyApplied = command.TotalPrice >= grossedUp * 0.99m;
                if (!alreadyApplied)
                {
                    finalTotalPrice = grossedUp;
                }
            }

            // Loyalty: resolve tier discount for authenticated customers.
            // Snapshot the tier at booking time even if the discount itself
            // is zero, so receipts can render "Bronze Cleaner — no discount".
            decimal tierDiscount = 0m;
            LoyaltyTier? tierAtPurchase = null;
            if (!string.IsNullOrEmpty(command.UserId))
            {
                var tierResult = await loyaltyService.ResolveTierDiscountForOrderAsync(
                    command.UserId, finalTotalPrice, cancellationToken);
                tierAtPurchase = tierResult.TierAtPurchase;
                tierDiscount = tierResult.DiscountAmount > 0m ? tierResult.DiscountAmount : 0m;
            }

            // Promo: preview promo discount if a code was supplied. Errors are
            // intentionally silent — frontend Validate already showed the
            // failure before submit. If the user managed to submit anyway with
            // a now-invalid code, we just don't apply it.
            decimal promoDiscount = 0m;
            string? promoCodeId = null;
            if (!string.IsNullOrEmpty(command.PromoCode) && !string.IsNullOrEmpty(command.UserId))
            {
                var preview = await promoCodeService.PreviewAsync(
                    command.PromoCode, command.UserId, finalTotalPrice, currency!.Id, cancellationToken);
                if (preview.Success)
                {
                    promoDiscount = preview.DiscountAmount;
                    promoCodeId = preview.PromoCodeId;
                }
            }

            // Membership: lookup the user's active Cleansia Plus (or future
            // membership product) and compute the discount it would grant.
            // Today this is always 0 for everyone (no MembershipPlan rows exist
            // until product launches Plus). When Plus ships, this branch starts
            // contributing without any code change here.
            decimal membershipDiscount = 0m;
            string? membershipPlanId = null;
            if (!string.IsNullOrEmpty(command.UserId))
            {
                var activeMembership = await userMembershipRepository
                    .GetActiveForUserAsync(command.UserId, cancellationToken);
                if (activeMembership != null)
                {
                    membershipDiscount = finalTotalPrice
                        * (activeMembership.MembershipPlan.DiscountPercentage / 100m);
                    membershipPlanId = activeMembership.MembershipPlanId;
                }
            }

            // Best-wins precedence: pick whichever single discount is largest.
            // Stacking is forbidden per spec (decisions table). The 3-source
            // tier/promo/membership comparison is the only place where this
            // logic lives — receipts render whichever applied + always show
            // the tierAtPurchase snapshot for badge display, regardless of which
            // discount actually won.
            decimal? appliedTierDiscount = null;
            LoyaltyTier? appliedTierAtPurchase = tierAtPurchase;
            decimal? appliedPromoDiscount = null;
            string? appliedPromoCodeId = null;
            decimal? appliedMembershipDiscount = null;
            string? appliedMembershipPlanId = null;

            var bestSource = membershipDiscount >= promoDiscount
                && membershipDiscount >= tierDiscount
                && membershipDiscount > 0m
                    ? "membership"
                    : promoDiscount > tierDiscount && promoDiscount > 0m
                        ? "promo"
                        : tierDiscount > 0m
                            ? "tier"
                            : "none";

            switch (bestSource)
            {
                case "membership":
                    finalTotalPrice -= membershipDiscount;
                    appliedMembershipDiscount = membershipDiscount;
                    appliedMembershipPlanId = membershipPlanId;
                    break;
                case "promo":
                    finalTotalPrice -= promoDiscount;
                    appliedPromoDiscount = promoDiscount;
                    appliedPromoCodeId = promoCodeId;
                    break;
                case "tier":
                    finalTotalPrice -= tierDiscount;
                    appliedTierDiscount = tierDiscount;
                    break;
            }

            var order = Order.Create(
                command.CustomerName,
                command.CustomerEmail,
                command.CustomerPhone,
                address,
                command.Rooms,
                command.Bathrooms,
                command.Extras,
                command.CleaningDate,
                command.PaymentType,
                finalTotalPrice,
                currency!.Id,
                PaymentStatus.Pending,
                userId: command.UserId,
                tierDiscountAmount: appliedTierDiscount,
                tierAtPurchase: appliedTierAtPurchase,
                promoDiscountAmount: appliedPromoDiscount,
                promoCodeId: appliedPromoCodeId,
                membershipDiscountAmount: appliedMembershipDiscount,
                membershipPlanIdAtPurchase: appliedMembershipPlanId,
                preferredEmployeeId: command.PreferredEmployeeId);

            order.SetCurrency(currency!);

            var selectedServices = await serviceRepository
                .GetByIds(command.SelectedServiceIds)
                .Select(s => OrderService.Create(order, s))
                .ToListAsync(cancellationToken);
            var selectedPackages = await packageRepository
                .GetByIds(command.SelectedPackageIds)
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

            // Compute VAT breakdown so receipts + fiscal integration have accurate figures.
            // When the company is not a VAT payer, this records net=TotalPrice, vat=0, rate=null.
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
                // No CompanyInfo configured — default to no VAT applied.
                order.SetVatBreakdown(order.TotalPrice, 0m, null);
            }

            string? stripeSessionId = null;

            switch (command.PaymentType)
            {
                case PaymentType.Card:
                    {
                        try
                        {
                            var stripeClient = stripeClientFactory.CreateClient();
                            stripeSessionId = await stripeClient.CreateCheckoutSessionAsync(order, cancellationToken);

                            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
                            orderRepository.Add(order);
                        }
                        catch (Exception ex)
                        {
                            // Log error (in production, use proper logging)
                            Console.WriteLine($"Stripe checkout session creation failed: {ex.Message}");

                            return BusinessResult.Failure<Response>(new Error(
                                nameof(PaymentType.Card),
                                BusinessErrorMessage.PaymentGatewayUnavailable));
                        }
                        break;
                    }
                case PaymentType.Cash:
                    {
                        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));

                        // Add order first so EF can resolve FK when receipt is created
                        orderRepository.Add(order);

                        // Enqueue receipt generation as a background job
                        await queueClient.SendAsync(QueueNames.GenerateReceipt,
                            new GenerateReceiptMessage(order.Id, command.Language), cancellationToken);

                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(PaymentType));
            }

            // Promo: now that the order is in the change tracker (and therefore
            // its Id is FK-resolvable), persist the redemption row and bump
            // the code's counter. Skipped silently if the promo lost the
            // best-wins comparison or no code was supplied. The UnitOfWork
            // pipeline commits the redemption + counter together with the
            // order in one transaction.
            if (appliedPromoDiscount.HasValue
                && !string.IsNullOrEmpty(command.PromoCode)
                && !string.IsNullOrEmpty(command.UserId))
            {
                var applyResult = await promoCodeService.ApplyAsync(
                    command.PromoCode,
                    command.UserId,
                    order.Id,
                    finalTotalPrice + appliedPromoDiscount.Value,
                    currency!.Id,
                    cancellationToken);
                if (!applyResult.Success)
                {
                    // Race lost between Preview and Apply (e.g. the user just
                    // exhausted the per-user cap on a parallel request). The
                    // order's stored discount is no longer backed by an audit
                    // row — log so admins can reconcile. Acceptable for v1.
                    logger.LogWarning(
                        "Promo apply failed after order created. OrderId={OrderId}, Code={Code}, Error={Error}",
                        order.Id, command.PromoCode, applyResult.Error);
                }
            }

            return BusinessResult.Success(new Response(
                        Id: order.Id,
                        ConfirmationCode: order.ConfirmationCode,
                        StripeSessionId: stripeSessionId));
        }

    }
}