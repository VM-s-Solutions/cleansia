using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Memberships;

[AuditAction("customer.membership.subscribe", Audience = AuditAudience.Customer, ResourceType = "UserMembership")]
public class CreateMembershipCheckoutSession
{
    /// <param name="CountryId">The market the customer is subscribing in (ADR-0058 D4); null is the platform default market.</param>
    public record Command(string PlanCode, string? CountryId = null) : ICommand<Response>;

    public record Response(string CheckoutUrl);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository)
        {
            RuleFor(x => x.PlanCode).NotEmpty().WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.CountryId)
                .MustAsync((countryId, ct) => countryRepository.IsServicedAsync(countryId!, ct))
                .WithMessage(BusinessErrorMessage.CountryNotServiced)
                .When(x => !string.IsNullOrEmpty(x.CountryId));

            // There is deliberately nothing here about the return URLs. They used to be two more
            // NotEmpty rules over two caller-supplied strings that went straight to Stripe's
            // success_url / cancel_url — an authenticated caller picked where the browser landed
            // after paying. Validating that input against an allow-list was the obvious fix and the
            // weaker one: it keeps a parser in the attack surface and still permits any path on an
            // allowed origin. The input is gone instead, derived server-side in StripeClient from
            // Stripe:SuccessUrlBase — which is what the ORDER checkout flow has always done.
        }
    }

    public class Handler(
        IUserRepository userRepository,
        IUserMembershipRepository userMembershipRepository,
        IMembershipPlanRepository membershipPlanRepository,
        IMembershipPlanPriceRepository membershipPlanPriceRepository,
        ICurrencyResolutionService currencyResolutionService,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        IStripeConfig stripeConfig,
        IMembershipTrialResolver membershipTrialResolver,
        IStripeCustomerResolver stripeCustomerResolver,
        IAuditContext auditContext,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {

            // Card payments can be switched off platform-wide, and a membership is a card charge like
            // any other — the first invoice bills immediately. Checked before any Stripe object is
            // created. -> IStripeConfig
            if (!stripeConfig.Enabled)
            {
                logger.LogWarning("Membership checkout session refused: card payments are disabled (Stripe:Enabled=false)");
                return BusinessResult.Failure<Response>(new Error(
                    "MembershipPlanId", BusinessErrorMessage.PaymentGatewayUnavailable));
            }
            var userId = userSessionProvider.GetUserId()!;
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(userId), BusinessErrorMessage.UserNotFound));
            }

            var plan = await membershipPlanRepository.GetByCodeAsync(command.PlanCode, cancellationToken);
            if (plan == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PlanCode), BusinessErrorMessage.MembershipPlanNotFound));
            }

            var existing = await userMembershipRepository.GetActiveForUserAsync(user.Id, cancellationToken);
            if (existing != null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PlanCode), BusinessErrorMessage.MembershipAlreadyActive));
            }

            var currency = await currencyResolutionService.ResolveCurrencyForCountryAsync(command.CountryId, cancellationToken);
            var price = await membershipPlanPriceRepository.GetForPlanAsync(plan.Id, currency.Id, cancellationToken);
            if (price == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PlanCode), BusinessErrorMessage.MembershipPlanNotPricedInCurrency));
            }

            // The Customer is per currency: Stripe locks a Customer to the currency of its first invoice,
            // so a re-subscribe in another market needs its own (owner ruling 2026-09-13).
            string stripeCustomerId;
            try
            {
                stripeCustomerId = await stripeCustomerResolver.ResolveForCurrencyAsync(user, currency, cancellationToken);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Stripe customer creation failed for user {UserId} (web checkout flow)", user.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PlanCode), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            // Fresh attempt id so re-opening checkout after abandoning yields
            // a new Session URL instead of replaying the (potentially expired)
            // original.
            var attemptId = Guid.NewGuid().ToString("N");
            var trial = await membershipTrialResolver.ResolveForUserAsync(user.Id, plan, cancellationToken);
            string url;
            try
            {
                url = await stripeClient.CreateMembershipCheckoutSessionAsync(
                    stripeCustomerId: stripeCustomerId,
                    stripePriceId: price.StripePriceId,
                    userId: user.Id,
                    membershipPlanCode: plan.Code,
                    trialPeriodDays: trial.Days,
                    idempotencyAttemptId: attemptId,
                    cancellationToken: cancellationToken);
            }
            catch (StripeException ex) when (StripeRefusals.IsCustomerCurrencyLocked(ex))
            {
                logger.LogWarning(ex,
                    "Stripe refused a {CurrencyCode} membership checkout for user {UserId}: the Stripe customer is locked to another currency",
                    currency.Code, user.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CountryId), BusinessErrorMessage.MembershipStripeCustomerCurrencyLocked));
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Stripe checkout session creation failed for user {UserId} (web checkout flow)", user.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PlanCode), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            // No membership row yet — the webhook provisions it — so the resource id stays null.
            auditContext.RecordEvidence("UserMembership", null, MembershipSubscribeEvidence.For(
                plan, price, currency.Code, command.CountryId, trial.Days, MembershipSubscribeChannel.Checkout));

            return BusinessResult.Success(new Response(url));
        }
    }
}
