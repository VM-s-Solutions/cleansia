using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Memberships;

public class CreateMembershipSubscription
{
    public record Command(string PlanCode, bool PaymentMethodConfirmed = false) : ICommand<Response>;

    public record Response(
        string MembershipId,
        string SetupIntentClientSecret,
        string StripeCustomerId,
        string EphemeralKey);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.PlanCode)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);
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
                stripeCustomerId = await stripeClient.CreateCustomerAsync(
                    user.Id,
                    user.Email,
                    $"{user.FirstName} {user.LastName}".Trim(),
                    user.PhoneNumber,
                    cancellationToken);
                user.AssignStripeCustomerId(stripeCustomerId);
                logger.LogInformation(
                    "Created Stripe customer {StripeCustomerId} for user {UserId} (subscribe flow)",
                    stripeCustomerId, user.Id);
            }

            if (command.PaymentMethodConfirmed)
            {
                // Fresh attempt id per call so re-subscribing after cancellation
                // creates a new Stripe subscription rather than replaying the
                // canceled one (idempotency-key collision).
                var attemptId = Guid.NewGuid().ToString("N");
                var subscription = await stripeClient.CreateSubscriptionAsync(
                    stripeCustomerId, plan.StripePriceId, plan.TrialPeriodDays, attemptId, cancellationToken);

                var membership = UserMembership.Create(
                    userId: user.Id,
                    membershipPlanId: plan.Id,
                    stripeSubscriptionId: subscription.SubscriptionId,
                    currentPeriodStart: subscription.CurrentPeriodStart,
                    currentPeriodEnd: subscription.CurrentPeriodEnd);
                userMembershipRepository.Add(membership);

                logger.LogInformation(
                    "Created UserMembership {MembershipId} (Stripe sub {SubscriptionId}) for user {UserId}, plan {PlanCode}",
                    membership.Id, subscription.SubscriptionId, user.Id, plan.Code);

                return BusinessResult.Success(new Response(
                    MembershipId: membership.Id,
                    SetupIntentClientSecret: string.Empty,
                    StripeCustomerId: stripeCustomerId,
                    EphemeralKey: string.Empty));
            }

            var setupIntent = await stripeClient.CreateSetupIntentAsync(stripeCustomerId, cancellationToken);
            var ephemeralKey = await stripeClient.CreateEphemeralKeyAsync(stripeCustomerId, cancellationToken);

            return BusinessResult.Success(new Response(
                MembershipId: string.Empty,
                SetupIntentClientSecret: setupIntent.ClientSecret,
                StripeCustomerId: stripeCustomerId,
                EphemeralKey: ephemeralKey));
        }
    }
}
