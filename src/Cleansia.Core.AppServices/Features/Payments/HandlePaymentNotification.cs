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

    public record Command(string JsonPayload, string SignatureHeader, string Language = Constants.Language.English) : ICommand<string>;

    public class Handler(IStripeConfig stripeConfig, IOrderRepository orderRepository, IEmailService emailService, IReceiptService receiptService, ILogger<Handler> logger) : ICommandHandler<Command, string>
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

            var result = await GenerateAndSendReceiptAsync(order, command.Language, cancellationToken);
            return result
                ? BusinessResult.Success(orderId)
                : BusinessResult.Failure<string>(new Error(nameof(emailService.SendOrderReceiptEmailAsync), BusinessErrorMessage.EmailNotSentError));
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