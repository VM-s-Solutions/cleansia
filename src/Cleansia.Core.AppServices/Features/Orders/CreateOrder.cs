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
using StripeException = Stripe.StripeException;
using Order = Cleansia.Core.Domain.Orders.Order;
using OrderService = Cleansia.Core.Domain.Orders.OrderService;

namespace Cleansia.Core.AppServices.Features.Orders;

public class CreateOrder
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderPricingCalculator _pricingCalculator;
        private readonly IOrderRepository _orderRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IPackageRepository packageRepository,
            IServiceRepository serviceRepository,
            ICurrencyRepository currencyRepository,
            IOrderPricingCalculator pricingCalculator,
            IOrderRepository orderRepository,
            IUserSessionProvider userSessionProvider)
        {
            _pricingCalculator = pricingCalculator;
            _orderRepository = orderRepository;
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

            When(x => !string.IsNullOrEmpty(x.PreferredEmployeeId)
                && !string.IsNullOrEmpty(_userSessionProvider.GetUserId()), () =>
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
                _userSessionProvider.GetUserId()!, command.PreferredEmployeeId!, cancellationToken);

        private static bool OrderMustNotBeEmpty(Command command) => command.SelectedPackageIds.Any() ||
                                                                    command.SelectedServiceIds.Any();

        private async Task<bool> PriceMatchesAsync(Command command, CancellationToken cancellationToken)
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
                cancellationToken);

            return result.TotalPrice == command.TotalPrice;
        }

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
        string? PreferredEmployeeId = null) : ICommand<Response>;

    public record Response(
        string Id,
        string ConfirmationCode,
        string? StripeSessionId);

    public class Handler(
        IAddressRepository addressRepository,
        ISavedAddressRepository savedAddressRepository,
        ICurrencyRepository currencyRepository,
        ICountryRepository countryRepository,
        IServiceCityRepository serviceCityRepository,
        IStripeClientFactory stripeClientFactory,
        IQueueClient queueClient,
        IPromoCodeService promoCodeService,
        IReferralService referralService,
        IReferralRepository referralRepository,
        IUserSessionProvider userSessionProvider,
        IOrderPricingCalculator pricingCalculator,
        IOrderFactory orderFactory,
        IAddressGeocoder addressGeocoder,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId() ?? string.Empty;

            // Late referral acceptance: a returning user who got the link
            // post-signup re-types it on the booking screen. Single retry,
            // logged on failure, never blocks the booking.
            if (!string.IsNullOrWhiteSpace(command.ReferralCode)
                && !string.IsNullOrEmpty(userId))
            {
                var existingReferral = await referralRepository.GetByReferredUserIdAsync(
                    userId, cancellationToken);
                if (existingReferral == null)
                {
                    try
                    {
                        var acceptResult = await referralService.AcceptAsync(
                            command.ReferralCode, userId, cancellationToken);
                        if (!acceptResult.IsAccepted)
                        {
                            logger.LogInformation(
                                "Late referral accept rejected for user {UserId}, code {Code}: {Error}",
                                userId, command.ReferralCode, acceptResult.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Failed late-accept referral code {Code} for user {UserId}",
                            command.ReferralCode, userId);
                    }
                }
            }

            // Address resolution — saved vs inline. SavedAddressId path enforces
            // ownership so a user can't book against another user's saved row.
            var addressResult = await ResolveAddressAsync(command, userId, cancellationToken);
            if (addressResult.Failure is { } failure)
            {
                return BusinessResult.Failure<Response>(failure);
            }
            var address = addressResult.Address!;

            // Customer orders must be in a city we actually serve. Employees
            // (UpdateEmployee / UpdateAddressInfo) are exempt — they can live
            // anywhere within a serviced country. The country itself has
            // already been validated as IsServiced by ResolveAddressAsync.
            if (!await serviceCityRepository.CityIsServicedAsync(
                address.CountryId, address.City, cancellationToken))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(address.City), BusinessErrorMessage.CityNotServiced));
            }

            if (address.Latitude is null || address.Longitude is null)
            {
                await addressGeocoder.PopulateCoordinatesAsync(address, cancellationToken);
            }

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
                cancellationToken);
            var rawSubtotal = calc.TotalPrice - calc.ExpressSurchargeAmount;

            // Promo preview lives outside the factory because it's a one-off
            // input (not a stored snapshot like tier/membership) and needs to
            // be Apply()d after the order persists, not just previewed.
            decimal promoDiscount = 0m;
            string? promoCodeId = null;
            if (!string.IsNullOrEmpty(command.PromoCode) && !string.IsNullOrEmpty(userId))
            {
                var preview = await promoCodeService.PreviewAsync(
                    command.PromoCode, userId, rawSubtotal, currency!.Id, cancellationToken);
                if (preview.Success)
                {
                    promoDiscount = preview.DiscountAmount;
                    promoCodeId = preview.PromoCodeId;
                }
            }

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
                PromoDiscountAmount: promoDiscount,
                PromoCodeId: promoCodeId,
                PreferredEmployeeId: command.PreferredEmployeeId,
                RecurringTemplateId: null), cancellationToken);

            // Stripe + receipt-queue side effects per payment type. Card
            // failures roll back to a Card-specific error code so the
            // client can show a "payment provider unavailable" copy.
            string? stripeSessionId = null;
            switch (command.PaymentType)
            {
                case PaymentType.Card:
                    try
                    {
                        var stripeClient = stripeClientFactory.CreateClient();
                        stripeSessionId = await stripeClient.CreateCheckoutSessionAsync(order, cancellationToken);
                    }
                    catch (StripeException ex)
                    {
                        // Narrow catch: only transient/API-level Stripe failures map to
                        // PaymentGatewayUnavailable. Anything else (DI misconfig, null ref,
                        // bad order state) should bubble as 500 so we see it, not mask it
                        // as a "gateway down" message to the user.
                        logger.LogError(ex, "Stripe checkout session creation failed");
                        return BusinessResult.Failure<Response>(new Error(
                            nameof(PaymentType.Card),
                            BusinessErrorMessage.PaymentGatewayUnavailable));
                    }
                    break;

                case PaymentType.Cash:
                    await queueClient.SendAsync(QueueNames.GenerateReceipt,
                        new GenerateReceiptMessage(order.Id, command.Language), cancellationToken);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(PaymentType));
            }

            // Promo persistence runs after the order is in the repo so the
            // promo row gets the order id. Failure logs but doesn't roll back —
            // the customer already paid and the promo just doesn't get tracked.
            if (promoDiscount > 0m
                && !string.IsNullOrEmpty(command.PromoCode)
                && !string.IsNullOrEmpty(userId))
            {
                var applyResult = await promoCodeService.ApplyAsync(
                    command.PromoCode,
                    userId,
                    order.Id,
                    order.TotalPrice + promoDiscount,
                    currency!.Id,
                    cancellationToken);
                if (!applyResult.Success)
                {
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

        private async Task<AddressResolution> ResolveAddressAsync(
            Command command, string userId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(command.SavedAddressId))
            {
                var saved = await savedAddressRepository.GetByIdAsync(command.SavedAddressId, cancellationToken);
                if (saved == null)
                {
                    return AddressResolution.Fail(new Error(
                        nameof(command.SavedAddressId), BusinessErrorMessage.NotFound));
                }
                if (!string.IsNullOrEmpty(userId) && saved.UserId != userId)
                {
                    return AddressResolution.Fail(new Error(
                        nameof(command.SavedAddressId), BusinessErrorMessage.NotFound));
                }
                var resolved = saved.Address
                    ?? await addressRepository.GetByIdAsync(saved.AddressId, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"SavedAddress {saved.Id} references missing Address {saved.AddressId}");

                // Edge case: address was saved while the country was
                // serviced, then admin de-flagged the country. Re-check
                // serviced status at order-time so stale saved rows can't
                // bypass the policy.
                if (!await countryRepository.IsServicedAsync(resolved.CountryId, cancellationToken))
                {
                    return AddressResolution.Fail(new Error(
                        nameof(command.SavedAddressId), BusinessErrorMessage.CountryNotServiced));
                }
                return AddressResolution.Ok(resolved);
            }

            var inline = command.CustomerAddress!;
            var resolvedCountryId = inline.CountryId;

            // Country must be supplied AND must be one we operate in. Old
            // behaviour (alphabetically default to "first in catalog") was
            // the source of the Argentina-for-CZ-addresses bug — silently
            // picking a wrong default is worse than failing loud.
            if (!string.IsNullOrEmpty(resolvedCountryId)
                && !await countryRepository.IsServicedAsync(resolvedCountryId, cancellationToken))
            {
                return AddressResolution.Fail(new Error(
                    nameof(inline.CountryId), BusinessErrorMessage.CountryNotServiced));
            }
            if (string.IsNullOrEmpty(resolvedCountryId))
            {
                // No country supplied. Fall back to the single serviced
                // country if there's exactly one; otherwise require the
                // client to pick.
                var servicedCountries = await countryRepository.GetServicedAsync(cancellationToken);
                if (servicedCountries.Count != 1)
                {
                    return AddressResolution.Fail(new Error(
                        nameof(inline.CountryId), BusinessErrorMessage.CountryRequired));
                }
                resolvedCountryId = servicedCountries[0].Id;
            }

            var address = await addressRepository.GetAddressAsync(
                inline.Street, inline.City, inline.ZipCode, resolvedCountryId, cancellationToken)
                ?? Address.Create(inline.Street, inline.City, inline.ZipCode, resolvedCountryId, inline.State);
            return AddressResolution.Ok(address);
        }

        private record AddressResolution(Address? Address, Error? Failure)
        {
            public static AddressResolution Ok(Address address) => new(address, null);
            public static AddressResolution Fail(Error error) => new(null, error);
        }
    }
}
