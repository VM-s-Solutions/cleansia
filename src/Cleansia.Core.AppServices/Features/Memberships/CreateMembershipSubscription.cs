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

/// <summary>
/// Subscribe the calling user to a Cleansia Plus plan. Two-phase flow:
///   1. If the user has no Stripe customer yet, create one. (Same lazy
///      creation as the one-off card payment path.)
///   2. Return a SetupIntent client_secret so the client (PaymentSheet on
///      mobile, Stripe Elements on web) can attach a payment method.
///   3. After the client confirms the SetupIntent (out of band), it calls
///      this same endpoint a second time with <see cref="Command.PaymentMethodConfirmed"/>
///      = true. This branch creates the actual Stripe subscription and the
///      local <see cref="UserMembership"/> row.
///
/// We split the flow because Stripe needs the payment method attached BEFORE
/// the subscription can be created with `default_incomplete` semantics. The
/// alternative (create-then-attach) requires more webhook plumbing for the
/// "incomplete" status and gives a worse UX.
/// </summary>
public class CreateMembershipSubscription
{
    public record Command(string PlanCode, bool PaymentMethodConfirmed = false, string UserId = "")
        : ICommand<Response>;

    /// <summary>
    /// Two response shapes wrapped in one record:
    ///  - When a payment method needs to be attached: <see cref="SetupIntentClientSecret"/> +
    ///    <see cref="StripeCustomerId"/> populated, <see cref="MembershipId"/> empty.
    ///  - After PaymentMethodConfirmed=true: <see cref="MembershipId"/> populated,
    ///    SetupIntentClientSecret empty.
    /// Discriminate via <see cref="MembershipId"/> being non-empty.
    /// </summary>
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

            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IUserRepository userRepository,
        IUserMembershipRepository userMembershipRepository,
        IMembershipPlanRepository membershipPlanRepository,
        IStripeClient stripeClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
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

            // Lazy Stripe customer creation, same as one-off card payment.
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
                    "Created Stripe customer {StripeCustomerId} for user {UserId} (subscribe flow)",
                    stripeCustomerId, user.Id);
            }

            // Phase 2 — payment method attached, create the subscription.
            if (command.PaymentMethodConfirmed)
            {
                var subscription = await stripeClient.CreateSubscriptionAsync(
                    stripeCustomerId, plan.StripePriceId, plan.TrialPeriodDays, cancellationToken);

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

            // Phase 1 — return SetupIntent so the client can attach a payment method.
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
