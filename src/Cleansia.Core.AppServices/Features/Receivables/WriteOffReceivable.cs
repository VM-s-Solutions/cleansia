using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Receivables;

/// <summary>
/// An administrator writes off what a customer owes on an order (owner ruling 2026-09-28, decision 18).
/// The customer may book cash again once nothing else is open. The note is required: it is the only
/// record of why money owed to the company stopped being owed. Its pay link is closed at Stripe first (owner
/// ruling 2026-10-06), so the customer cannot pay what was just written off; a link Stripe reports paid, or a
/// Stripe that cannot be reached, refuses the write-off.
/// </summary>
[AuditAction("receivable.write_off", Sensitive = true, ResourceType = "Receivable")]
public class WriteOffReceivable
{
    public record Command(string ReceivableId, string Note) : ICommand<Response>;

    public record Response(string ReceivableId, ReceivableStatus Status);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ReceivableId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.Note)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IReceivableRepository receivableRepository,
        IStripeClient stripeClient,
        IUserSessionProvider userSessionProvider,
        TimeProvider timeProvider,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var receivable = await receivableRepository.GetByIdAsync(command.ReceivableId, cancellationToken);
            if (receivable is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ReceivableId), BusinessErrorMessage.ReceivableNotFound));
            }

            if (!receivable.IsOpen)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ReceivableId), BusinessErrorMessage.ReceivableNotOpen));
            }

            if (receivable.PayLinkSessionId is { } payLink)
            {
                bool closed;
                try
                {
                    closed = await stripeClient.ExpireReceivableCheckoutSessionAsync(payLink, cancellationToken);
                }
                catch (StripeException ex)
                {
                    logger.LogWarning(ex,
                        "Pay link {SessionId} of receivable {ReceivableId} could not be closed; it is not written off",
                        payLink, receivable.Id);
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.ReceivableId), BusinessErrorMessage.PaymentGatewayUnavailable));
                }

                if (!closed)
                {
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.ReceivableId), BusinessErrorMessage.ReceivableNotOpen));
                }
            }

            receivable.WriteOff(userSessionProvider.GetUserId()!, command.Note, timeProvider.GetUtcNow());

            return BusinessResult.Success(new Response(receivable.Id, receivable.Status));
        }
    }
}
