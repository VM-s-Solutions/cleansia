using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.SendGrid;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
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
                .NotEmpty().WithMessage(BusinessErrorMessage.JsonPayloadRequired);

            RuleFor(x => x.SignatureHeader)
                .NotEmpty().WithMessage(BusinessErrorMessage.StripeSignatureRequired);

            RuleFor(x => x)
                .MustAsync(OrderExistsAsync)
                .When(NotificationIsHandled);
        }

        private bool NotificationIsHandled(Command command)
        {
            var stripeEvent = EventUtility.ConstructEvent(command.JsonPayload, command.SignatureHeader, _stripeConfig.WebhookSecret);
            return stripeEvent.Type is Constants.StripeEventType.CompletedSession
                                   or Constants.StripeEventType.ExpiredSession;
        }

        private async Task<bool> OrderExistsAsync(Command command, CancellationToken cancellationToken)
        {
            var stripeEvent = EventUtility.ConstructEvent(command.JsonPayload, command.SignatureHeader, _stripeConfig.WebhookSecret);
            if (stripeEvent.Type is not (Constants.StripeEventType.CompletedSession
                                     or Constants.StripeEventType.ExpiredSession))
            {
                return true;
            }

            var session = stripeEvent.Data.Object as Session;
            var orderId = session?.Metadata["OrderId"];

            return !string.IsNullOrWhiteSpace(orderId) && await _orderRepository.ExistsAsync(orderId, cancellationToken);
        }
    }

    public record Command(string JsonPayload, string SignatureHeader, string Language = Constants.Language.English) : ICommand<string>;

    public class Handler(IStripeConfig stripeConfig, IOrderRepository orderRepository, IEmailService emailService, IReceiptService receiptService, ILogger<Handler> logger) : ICommandHandler<Command, string>
    {
        public async Task<BusinessResult<string>> Handle(Command command, CancellationToken cancellationToken)
        {
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(command.JsonPayload, command.SignatureHeader, stripeConfig.WebhookSecret);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Invalid webhook signature");
                return BusinessResult.Failure<string>(new Error(
                    "InvalidSignature",
                    "Invalid webhook signature"));
            }

            if (stripeEvent.Type is not (Constants.StripeEventType.CompletedSession
                                    or Constants.StripeEventType.ExpiredSession))
            {
                logger.LogInformation("Received webhook event type {EventType}, ignoring", stripeEvent.Type);
                return BusinessResult.Success(string.Empty);
            }

            var session = stripeEvent.Data.Object as Session;
            var orderId = session?.Metadata?["OrderId"];

            if (string.IsNullOrEmpty(orderId))
            {
                logger.LogError("Order ID not found in webhook metadata");
                return BusinessResult.Failure<string>(new Error(
                    "OrderIdMissing",
                    "Order ID not found in webhook metadata"));
            }

            var order = await orderRepository.GetByIdAsync(orderId, cancellationToken);
            if (order == null)
            {
                logger.LogError("Order {OrderId} not found", orderId);
                return BusinessResult.Failure<string>(new Error(
                    nameof(orderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            if (stripeEvent.Type == Constants.StripeEventType.ExpiredSession)
            {
                return await HandleExpiredSession(order, orderId, cancellationToken);
            }

            return await HandleCompletedSession(order, orderId, command.Language, cancellationToken);
        }

        private async Task<BusinessResult<string>> HandleCompletedSession(Order order, string orderId, string language, CancellationToken cancellationToken)
        {
            // Idempotency check - don't process if already paid
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                logger.LogInformation("Order {OrderId} already marked as paid, skipping webhook processing", orderId);
                return BusinessResult.Success(orderId);
            }

            // Update payment status
            order.UpdatePaymentStatus(PaymentStatus.Paid);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

            // Try to generate and send receipt, but don't fail webhook if it fails
            try
            {
                await GenerateAndSendReceiptAsync(order, language, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate/send receipt for order {OrderId}, will retry later", orderId);
            }

            logger.LogInformation("Successfully processed payment webhook for order {OrderId}", orderId);
            return BusinessResult.Success(orderId);
        }

        private async Task<BusinessResult<string>> HandleExpiredSession(Order order, string orderId, CancellationToken cancellationToken)
        {
            // Idempotency check - don't process if already cancelled or paid
            if (order.PaymentStatus is PaymentStatus.Failed or PaymentStatus.Paid)
            {
                logger.LogInformation("Order {OrderId} already has payment status {Status}, skipping expired session", orderId, order.PaymentStatus);
                return BusinessResult.Success(orderId);
            }

            order.UpdatePaymentStatus(PaymentStatus.Failed);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

            logger.LogInformation("Cancelled order {OrderId} due to expired Stripe checkout session", orderId);
            return BusinessResult.Success(orderId);
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
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate and send receipt for order {OrderId}", order.Id);
                return false;
            }
        }
    }
}