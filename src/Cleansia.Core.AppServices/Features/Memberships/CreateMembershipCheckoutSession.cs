using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// Web-only subscribe path. Returns a Stripe Checkout Session URL the
/// customer is redirected to. Stripe collects the payment method and creates
/// the subscription server-side; the local <see cref="UserMembership"/> row
/// is provisioned by the <c>customer.subscription.created</c> webhook (not
/// here) so we don't need any client-side polling on the success URL.
///
/// Distinct from <see cref="CreateMembershipSubscription"/> which is the
/// mobile (PaymentSheet + SetupIntent) path. The web app doesn't ship Stripe
/// Elements, and reusing the redirect-Checkout pattern matches the existing
/// one-off order flow.
/// </summary>
public class CreateMembershipCheckoutSession
{
    public record Command(string PlanCode, string SuccessUrl, string CancelUrl, string UserId = "")
        : ICommand<Response>;

    public record Response(string CheckoutUrl);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PlanCode).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.UserId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.SuccessUrl).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.CancelUrl).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IUserRepository userRepository,
        IUserMembershipRepository userMembershipRepository,
        IMembershipPlanRepository membershipPlanRepository,
        IStripeClient stripeClient,
        IStripeConfig stripeConfig,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            _ = stripeConfig; // reserved for future per-env URL fallbacks; keeps DI binding.
            var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);
            if (user == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.UserId), BusinessErrorMessage.UserNotFound));
            }

            var plan = await membershipPlanRepository.GetByCodeAsync(command.PlanCode, cancellationToken);
            if (plan == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PlanCode), BusinessErrorMessage.MembershipPlanNotFound));
            }

            // Idempotency: bail if user already has an active membership.
            var existing = await userMembershipRepository.GetActiveForUserAsync(user.Id, cancellationToken);
            if (existing != null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(UserMembership), BusinessErrorMessage.MembershipAlreadyActive));
            }

            // Lazy Stripe customer creation, same pattern as the other paths.
            var stripeCustomerId = user.StripeCustomerId;
            if (string.IsNullOrEmpty(stripeCustomerId))
            {
                stripeCustomerId = await stripeClient.CreateCustomerAsync(
                    user.Email,
                    $"{user.FirstName} {user.LastName}".Trim(),
                    user.PhoneNumber,
                    cancellationToken);
                user.AssignStripeCustomerId(stripeCustomerId);
                logger.LogInformation(
                    "Created Stripe customer {StripeCustomerId} for user {UserId} (web checkout flow)",
                    stripeCustomerId, user.Id);
            }

            var url = await stripeClient.CreateMembershipCheckoutSessionAsync(
                stripeCustomerId: stripeCustomerId,
                stripePriceId: plan.StripePriceId,
                userId: user.Id,
                membershipPlanCode: plan.Code,
                trialPeriodDays: plan.TrialPeriodDays,
                successUrl: command.SuccessUrl,
                cancelUrl: command.CancelUrl,
                cancellationToken: cancellationToken);

            return BusinessResult.Success(new Response(url));
        }
    }
}
