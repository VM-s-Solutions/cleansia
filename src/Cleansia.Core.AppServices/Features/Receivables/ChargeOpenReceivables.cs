using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Receivables;

/// <summary>
/// Charges each open receivable once to the customer's saved card, with the customer absent (owner ruling
/// 2026-09-28, decisions 16 to 18) — and does nothing at all unless <c>Payments:OffSessionChargesEnabled</c>
/// is on. It stays off: switching it on would contradict the owner's ruling of 2026-10-04 that no saved card
/// is charged for a fee or unpaid cash, so it is not a step waiting on the lawyer's wording. Even switched
/// on, it charges only a card saved under a <c>card-guarantee-*</c> consent. The outcome arrives by
/// webhook: a success settles the receivable, a decline or an authentication demand e-mails the customer
/// a pay link. A pay link the customer holds is closed before the charge, and one they have already paid is
/// not charged again. A customer with no such card in the currency is not charged, and the receivable
/// stays open for their own pay link and the administrators.
/// </summary>
public class ChargeOpenReceivables
{
    public record Command : ICommand<Response>;

    public class Validator : AbstractValidator<Command>;

    public record Response(int Attempted, int Charged);

    private const int BatchSize = 50;

    /// <summary>
    /// Only the card-guarantee consents ever told the customer a saved card could be charged without them;
    /// the consent printed with Save this card since <c>saved-card-draft-2026-10-05</c> promises it never is
    /// unless they pay with it.
    /// </summary>
    private const string CardGuaranteeConsentPrefix = "card-guarantee-";

    public class Handler(
        IPaymentsConfig paymentsConfig,
        IStripeConfig stripeConfig,
        IReceivableRepository receivableRepository,
        ISavedCardRepository savedCardRepository,
        IStripeClientFactory stripeClientFactory,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            if (!paymentsConfig.OffSessionChargesEnabled)
            {
                return BusinessResult.Success(new Response(0, 0));
            }

            if (!stripeConfig.Enabled)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(stripeConfig.Enabled), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            var stripe = stripeClientFactory.CreateClient();
            var now = timeProvider.GetUtcNow();
            var attempted = 0;
            var charged = 0;

            foreach (var receivable in await receivableRepository.GetUnchargedOpenIgnoringTenantAsync(BatchSize, cancellationToken))
            {
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(receivable.TenantId))
                {
                    tenantProvider.SetTenantOverride(receivable.TenantId);
                }

                var card = (await savedCardRepository.GetCapturedForUserInCurrencyAsync(
                        receivable.UserId, receivable.CurrencyId, cancellationToken))
                    .FirstOrDefault(c => c.IsUsableOn(now)
                        && c.ConsentTextVersion.StartsWith(CardGuaranteeConsentPrefix, StringComparison.Ordinal));

                // The attempt is committed before the charge, so no later run charges it again whatever
                // happens to the call; the attempt number keys the charge, so a retry of it replays it.
                receivable.RecordChargeAttempt();
                await unitOfWork.CommitAsync(cancellationToken);
                attempted++;

                if (card is null)
                {
                    logger.LogWarning(
                        "Receivable {ReceivableId} has no usable card in its currency saved under a card-guarantee consent; left open for the customer's pay link",
                        receivable.Id);
                    continue;
                }

                try
                {
                    if (receivable.PayLinkSessionId is { } payLink
                        && !await stripe.ExpireReceivableCheckoutSessionAsync(payLink, cancellationToken))
                    {
                        logger.LogInformation(
                            "Receivable {ReceivableId} was paid through its pay link {SessionId}; not charged",
                            receivable.Id, payLink);
                        continue;
                    }

                    var paymentIntentId = await stripe.ChargeReceivableOffSessionAsync(
                        receivable.Id,
                        receivable.Amount,
                        receivable.Currency!.Code,
                        card.StripeCustomerId,
                        card.StripePaymentMethodId!,
                        receivable.Attempts,
                        cancellationToken);
                    charged++;
                    logger.LogInformation(
                        "Charged receivable {ReceivableId} off-session as {PaymentIntentId}", receivable.Id, paymentIntentId);
                }
                catch (StripeException ex)
                {
                    logger.LogWarning(ex,
                        "Off-session charge of receivable {ReceivableId} not taken ({FailureCode}); a decline's failure webhook e-mails the pay link",
                        receivable.Id, ex.StripeError?.Code);
                }
            }

            return BusinessResult.Success(new Response(attempted, charged));
        }
    }
}
