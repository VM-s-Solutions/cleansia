using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

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
        ICurrencyRepository currencyRepository,
        IUserSessionProvider userSessionProvider,
        IOrderPricingCalculator pricingCalculator,
        IOrderFactory orderFactory,
        IOrderAddressResolver orderAddressResolver,
        IOrderPromoApplier orderPromoApplier,
        IOrderLateReferralAcceptor orderLateReferralAcceptor,
        IOrderPaymentDispatcher orderPaymentDispatcher) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId() ?? string.Empty;

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
                cancellationToken);
            var rawSubtotal = calc.TotalPrice - calc.ExpressSurchargeAmount;

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
                PromoDiscountAmount: promo.DiscountAmount,
                PromoCodeId: promo.PromoCodeId,
                PreferredEmployeeId: command.PreferredEmployeeId,
                RecurringTemplateId: null), cancellationToken);

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
                command, userId, order, promo, currency!.Id, cancellationToken);

            return BusinessResult.Success(new Response(
                Id: order.Id,
                ConfirmationCode: order.ConfirmationCode,
                StripeSessionId: dispatch.StripeSessionId));
        }
    }
}
