using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Stripe;
using Stripe.Checkout;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Payments;

public class HandlePaymentNotification
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IStripeConfig _stripeConfig;
        private readonly IOrderRepository _orderRepository;

        public Validator(IStripeConfig stripeConfig, IOrderRepository orderRepository)
        {
            _stripeConfig = stripeConfig;
            _orderRepository = orderRepository;

            RuleFor(x => x.JsonPayload)
                .NotEmpty().WithMessage("JSON payload is required.");

            RuleFor(x => x.SignatureHeader)
                .NotEmpty().WithMessage("Stripe signature header is required.");

            RuleFor(x => x)
                .MustAsync(OrderExistsAsync)
                .When(NotificationIsCompleted);
        }

        private bool NotificationIsCompleted(Command command)
        {
            var stripeEvent = EventUtility.ConstructEvent(command.JsonPayload, command.SignatureHeader, _stripeConfig.WebhookSecret);
            return stripeEvent.Type == Constants.StripeEventType.CompletedSession;
        }

        private async Task<bool> OrderExistsAsync(Command command, CancellationToken cancellationToken)
        {
            var stripeEvent = EventUtility.ConstructEvent(command.JsonPayload, command.SignatureHeader, _stripeConfig.WebhookSecret);
            if (stripeEvent.Type != Constants.StripeEventType.CompletedSession)
            {
                return true;
            }

            var session = stripeEvent.Data.Object as Session;
            var orderId = session?.Metadata["OrderId"];

            return !string.IsNullOrWhiteSpace(orderId) && await _orderRepository.ExistsAsync(orderId, cancellationToken);
        }
    }

    public record Command(
        string JsonPayload,
        string SignatureHeader) : ICommand<string>;

    public class Handler(
        IStripeConfig stripeConfig,
        ISendGridConfig sendGridConfig,
        IOrderRepository orderRepository,
        ISendGridClientFactory clientFactory) : ICommandHandler<Command, string>
    {
        public async Task<BusinessResult<string>> Handle(Command command, CancellationToken cancellationToken)
        {
            var stripeEvent = EventUtility.ConstructEvent(command.JsonPayload, command.SignatureHeader, stripeConfig.WebhookSecret);

            if (stripeEvent.Type != Constants.StripeEventType.CompletedSession)
            {
                return BusinessResult.Success(string.Empty);
            }

            var session = stripeEvent.Data.Object as Session;
            var orderId = session!.Metadata["OrderId"]!;

            var order = await orderRepository.GetByIdAsync(orderId, cancellationToken);
            order!.UpdatePaymentStatus(PaymentStatus.Paid);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

            var result = await SendConfirmationEmailAsync(order, cancellationToken);
            return result
                ? BusinessResult.Success(orderId)
                : BusinessResult.Failure<string>(new Error(nameof(clientFactory.SendTemplateEmailAsync),
                    BusinessErrorMessage.EmailNotSentError));
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