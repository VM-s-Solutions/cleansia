using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Receivables;

/// <summary>
/// The customer pays what they owe on an order through a pay link: a payment-mode Stripe Checkout Session
/// for the receivable's amount (owner ruling 2026-09-28, decision 18). It works whether off-session
/// charging is on or off, which is how a receivable is settled while the charges stay switched off. The
/// link is recorded on the receivable, so the same one is handed out while it is open and the off-session
/// charge can close it first. The checkout.session.completed webhook marks it paid.
/// </summary>
public class CreateReceivablePayLink
{
    public record Command(string ReceivableId) : ICommand<Response>;

    public record Response(string ReceivableId, string CheckoutUrl);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ReceivableId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IReceivableRepository receivableRepository,
        IUserSessionProvider userSessionProvider,
        IStripeConfig stripeConfig,
        IStripeClient stripeClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var receivable = await receivableRepository.GetByIdIgnoringTenantAsync(command.ReceivableId, cancellationToken);
            if (receivable is null || receivable.UserId != userSessionProvider.GetUserId())
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ReceivableId), BusinessErrorMessage.ReceivableNotFound));
            }

            if (!receivable.IsOpen)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ReceivableId), BusinessErrorMessage.ReceivableNotOpen));
            }

            if (!stripeConfig.Enabled)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ReceivableId), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            CheckoutSessionResult link;
            try
            {
                link = await stripeClient.CreateReceivableCheckoutSessionAsync(
                    receivable.Id,
                    receivable.PayLinkSessionId,
                    receivable.OrderId,
                    receivable.Order!.DisplayOrderNumber,
                    receivable.Amount,
                    receivable.Currency!.Code,
                    cancellationToken);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Stripe pay link failed for receivable {ReceivableId}", receivable.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ReceivableId), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            receivable.RecordPayLink(link.Id);
            return BusinessResult.Success(new Response(receivable.Id, link.Url));
        }
    }
}
