using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Memberships;

public class CreateMembershipCheckoutSession
{
    public record Command(string PlanCode, string SuccessUrl, string CancelUrl) : ICommand<Response>;

    public record Response(string CheckoutUrl);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PlanCode).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.SuccessUrl).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.CancelUrl).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IUserRepository userRepository,
        IUserMembershipRepository userMembershipRepository,
        IMembershipPlanRepository membershipPlanRepository,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(Command), BusinessErrorMessage.UserNotFound));
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
                    nameof(UserMembership), BusinessErrorMessage.MembershipAlreadyActive));
            }

            var stripeCustomerId = user.StripeCustomerId;
            if (string.IsNullOrEmpty(stripeCustomerId))
            {
                try
                {
                    stripeCustomerId = await stripeClient.CreateCustomerAsync(
                        user.Id,
                        user.Email,
                        $"{user.FirstName} {user.LastName}".Trim(),
                        user.PhoneNumber,
                        cancellationToken);
                }
                catch (StripeException ex)
                {
                    logger.LogError(ex, "Stripe customer creation failed for user {UserId} (web checkout flow)", user.Id);
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.PlanCode), BusinessErrorMessage.PaymentGatewayUnavailable));
                }
                user.AssignStripeCustomerId(stripeCustomerId);
                logger.LogInformation(
                    "Created Stripe customer {StripeCustomerId} for user {UserId} (web checkout flow)",
                    stripeCustomerId, user.Id);
            }

            // Fresh attempt id so re-opening checkout after abandoning yields
            // a new Session URL instead of replaying the (potentially expired)
            // original.
            var attemptId = Guid.NewGuid().ToString("N");
            string url;
            try
            {
                url = await stripeClient.CreateMembershipCheckoutSessionAsync(
                    stripeCustomerId: stripeCustomerId,
                    stripePriceId: plan.StripePriceId,
                    userId: user.Id,
                    membershipPlanCode: plan.Code,
                    trialPeriodDays: plan.TrialPeriodDays,
                    successUrl: command.SuccessUrl,
                    cancelUrl: command.CancelUrl,
                    idempotencyAttemptId: attemptId,
                    cancellationToken: cancellationToken);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Stripe checkout session creation failed for user {UserId} (web checkout flow)", user.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PlanCode), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            return BusinessResult.Success(new Response(url));
        }
    }
}
