using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Order = Cleansia.Core.Domain.Orders.Order;
using OrderService = Cleansia.Core.Domain.Orders.OrderService;

namespace Cleansia.Core.AppServices.Features.Orders;

public class CreateOrder
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IPackageRepository _packageRepository;
        private readonly IServiceRepository _serviceRepository;
        private readonly ICurrencyRepository _currencyRepository;

        public Validator(
            IPackageRepository packageRepository,
            IServiceRepository serviceRepository,
            ICurrencyRepository currencyRepository)
        {
            _packageRepository = packageRepository;
            _serviceRepository = serviceRepository;
            _currencyRepository = currencyRepository;

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

            RuleFor(x => x.CustomerAddress.Street)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(5)
                .WithMessage(BusinessErrorMessage.MinLength)
                .MaximumLength(255)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.CustomerAddress.City)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(2)
                .WithMessage(BusinessErrorMessage.MinLength)
                .MaximumLength(100)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.CustomerAddress.ZipCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(3)
                .WithMessage(BusinessErrorMessage.MinLength)
                .MaximumLength(20)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.CleaningDate)
                .GreaterThan(DateTime.UtcNow)
                .WithMessage(BusinessErrorMessage.CleaningDateInFuture);

            RuleFor(x => x.TotalPrice)
                .GreaterThan(0)
                .WithMessage(BusinessErrorMessage.TotalPriceMustBePositive);

            When(x => !string.IsNullOrEmpty(x.CurrencyId), () =>
            {
                RuleFor(x => x.CurrencyId!)
                    .MustAsync(currencyRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.InvalidCurrency);
            });

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
        }

        private static bool OrderMustNotBeEmpty(Command command) => command.SelectedPackageIds.Any() ||
                                                                    command.SelectedServiceIds.Any();

        private async Task<bool> PriceMatchesAsync(Command command, CancellationToken cancellationToken)
        {
            decimal? basePrice = 0.0M;

            var packages = await _packageRepository.GetByIds(command.SelectedPackageIds).ToListAsync(cancellationToken);
            basePrice += packages.Sum(p => p.Price);

            var services = await _serviceRepository.GetByIds(command.SelectedServiceIds).ToListAsync(cancellationToken);
            basePrice += services.Sum(s => s?.BasePrice + s?.PerRoomPrice * (command.Rooms + command.Bathrooms));

            var currency = string.IsNullOrEmpty(command.CurrencyId)
                ? await _currencyRepository.GetDefaultAsync(cancellationToken)
                : await _currencyRepository.GetByIdAsync(command.CurrencyId, cancellationToken);
            return basePrice * currency?.ExchangeRate == command.TotalPrice;
        }
    }

    public record Command(
        string CustomerName,
        string CustomerEmail,
        string CustomerPhone,
        AddressDto CustomerAddress,
        IEnumerable<string> SelectedPackageIds,
        IEnumerable<string> SelectedServiceIds,
        int Rooms,
        int Bathrooms,
        Dictionary<string, bool> Extras,
        DateTime CleaningDate,
        PaymentType PaymentType,
        string? CurrencyId,
        decimal TotalPrice,
        string Language = Constants.Language.English) : ICommand<Response>;

    public record Response(
        string Id,
        string ConfirmationCode,
        string? StripeSessionId);

    public class Handler(
        ISendGridConfig sendGridConfig,
        IOrderRepository orderRepository,
        IAddressRepository addressRepository,
        IServiceRepository serviceRepository,
        IPackageRepository packageRepository,
        ICurrencyRepository currencyRepository,
        ICountryRepository countryRepository,
        ISendGridClientFactory clientFactory,
        IStripeClientFactory stripeClientFactory,
        IEmailService emailService,
        IReceiptService receiptService) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Resolve country — verify provided CountryId exists, or fall back to default
            var countryId = command.CustomerAddress.CountryId;
            if (!string.IsNullOrEmpty(countryId) && !await countryRepository.ExistsAsync(countryId, cancellationToken))
            {
                countryId = null; // Invalid ID, fall back to default
            }
            if (string.IsNullOrEmpty(countryId))
            {
                var defaultCountry = await countryRepository.GetByIsoCodeAsync("CZE", cancellationToken)
                    ?? await countryRepository.GetQueryable().FirstOrDefaultAsync(cancellationToken);
                countryId = defaultCountry?.Id ?? throw new InvalidOperationException("No countries configured in the system");
            }

            var address = await addressRepository.GetAddressAsync(command.CustomerAddress.Street,
                command.CustomerAddress.City, command.CustomerAddress.ZipCode, countryId,
                cancellationToken) ?? Address.Create(
                command.CustomerAddress.Street,
                command.CustomerAddress.City,
                command.CustomerAddress.ZipCode,
                countryId,
                command.CustomerAddress.State);

            var currency = string.IsNullOrEmpty(command.CurrencyId)
                ? await currencyRepository.GetDefaultAsync(cancellationToken)
                : await currencyRepository.GetByIdAsync(command.CurrencyId, cancellationToken);

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
                command.TotalPrice,
                currency!.Id,
                PaymentStatus.Pending);

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

            string? stripeSessionId = null;

            switch (command.PaymentType)
            {
                case PaymentType.Card:
                    {
                        try
                        {
                            var stripeClient = stripeClientFactory.CreateClient();
                            stripeSessionId = await stripeClient.CreateCheckoutSessionAsync(order, cancellationToken);

                            // Order starts as Pending until Stripe webhook confirms payment
                            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Pending, order));
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
                        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

                        // Add order first so EF can resolve FK when receipt is created
                        orderRepository.Add(order);

                        // Generate receipt and send email for cash payments
                        var receiptResult = await GenerateAndSendReceiptAsync(order, command.Language, cancellationToken);
                        if (!receiptResult)
                        {
                            return BusinessResult.Failure<Response>(new Error(
                                nameof(emailService.SendOrderReceiptEmailAsync),
                                BusinessErrorMessage.EmailNotSentError));
                        }

                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(PaymentType));
            }

            return BusinessResult.Success(new Response(
                        Id: order.Id,
                        ConfirmationCode: order.ConfirmationCode,
                        StripeSessionId: stripeSessionId));
        }

        private async Task<bool> GenerateAndSendReceiptAsync(Order order, string languageCode, CancellationToken cancellationToken)
        {
            try
            {
                var receipt = await receiptService.GenerateReceiptAsync(order, languageCode, cancellationToken);
                var pdfBytes = await receiptService.DownloadReceiptPdfAsync(receipt, cancellationToken);

                var messageId = await emailService.SendOrderReceiptEmailAsync(order.CustomerEmail, order, pdfBytes, receipt.FileName, languageCode, cancellationToken);
                receipt.MarkEmailSent(messageId);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}