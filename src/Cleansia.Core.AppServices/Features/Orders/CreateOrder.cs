using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
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

            RuleFor(x => x.CustomerEmail)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .EmailAddress()
                .WithMessage(BusinessErrorMessage.InvalidEmailFormat);

            RuleFor(x => x.CleaningDate)
                .GreaterThan(DateTime.UtcNow)
                .WithMessage(BusinessErrorMessage.CleaningDateInFuture);

            RuleFor(x => x.TotalPrice)
                .GreaterThan(0)
                .WithMessage(BusinessErrorMessage.TotalPriceMustBePositive);

            RuleFor(x => x.CurrencyId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(currencyRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvalidCurrency);

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

            var currency = await _currencyRepository.GetByIdAsync(command.CurrencyId, cancellationToken);
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
        string CurrencyId,
        decimal TotalPrice) : ICommand<Response>;

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
        ISendGridClientFactory clientFactory,
        IStripeClientFactory stripeClientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var address = await addressRepository.GetAddressAsync(command.CustomerAddress.Street,
                command.CustomerAddress.City, command.CustomerAddress.ZipCode, command.CustomerAddress.CountryId,
                cancellationToken) ?? Address.Create(
                command.CustomerAddress.Street,
                command.CustomerAddress.City,
                command.CustomerAddress.ZipCode,
                command.CustomerAddress.CountryId);

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
                command.CurrencyId,
                PaymentStatus.Pending);

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
            orderRepository.Add(order);

            string? stripeSessionId = null;

            switch (command.PaymentType)
            {
                case PaymentType.Card:
                    {
                        var stripeClient = stripeClientFactory.CreateClient();
                        stripeSessionId = await stripeClient.CreateCheckoutSessionAsync(order, cancellationToken);
                        break;
                    }
                case PaymentType.Cash:
                    order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
                    var result = await SendConfirmationEmailAsync(order, cancellationToken);
                    if (!result)
                    {
                        return BusinessResult.Failure<Response>(new Error(nameof(clientFactory.SendTemplateEmailAsync), BusinessErrorMessage.EmailNotSentError));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(PaymentType));
            }

            return BusinessResult.Success(new Response(
                        Id: order.Id,
                        ConfirmationCode: order.ConfirmationCode,
                        StripeSessionId: stripeSessionId));
        }

        private async Task<bool> SendConfirmationEmailAsync(Order order, CancellationToken cancellationToken)
        {
            const int emailSendRetries = 3;
            var sendGridClient = clientFactory.CreateClient();
            for (var i = 0; i < emailSendRetries; i++)
            {
                var result = await clientFactory.SendTemplateEmailAsync(sendGridClient, sendGridConfig.AddressFrom, order.CustomerEmail, sendGridConfig.OrderReceiptTemplateId, order, cancellationToken);
                if (result.IsSuccess)
                {
                    return true;
                }
            }

            return false;
        }
    }
}