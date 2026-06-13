using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// The native-SDK membership subscribe flow (SetupIntent + EphemeralKey, then a confirmed
/// PaymentSheet subscribe). This is deliberately ONE of two subscribe surfaces, and they do not
/// share a path:
/// <list type="bullet">
/// <item>WEB subscribes via Stripe-hosted Checkout — <c>CreateMembershipCheckoutSession</c>, driven
/// by the web <c>MembershipFacade.createCheckoutSession</c>. The web facade never calls this handler.</item>
/// <item>MOBILE subscribes via this handler's SetupIntent/PaymentSheet flow — it is the SOLE consumer
/// of <c>MembershipController.Subscribe</c> (wired on both the web-customer and mobile-customer APIs).
/// The endpoint is therefore NOT orphaned despite the web facade not calling it.</item>
/// </list>
/// The split is intentional and the endpoint is gated by <c>[Permission(Policy.CanManageMembership)]</c>
/// and rate-limited. The confirmed branch is idempotent on a client-supplied idempotency token.
/// </summary>
public class CreateMembershipSubscription
{
    public record Command(string PlanCode, bool PaymentMethodConfirmed = false) : ICommand<Response>
    {
        /// <summary>
        /// Client-supplied idempotency token for the confirmed-subscribe (Phase-2) path. The mobile
        /// app generates it ONCE per subscribe attempt (at Phase-1 / startSubscribe) and resends the
        /// SAME value on every Phase-2 confirm retry (double-tapped PaymentSheet, network retry). The
        /// handler derives the Stripe idempotency key from it so a retried/concurrent confirm REPLAYS
        /// the same Stripe subscription instead of creating a second one. Nullable:
        /// web and not-yet-updated callers omit it, and the handler derives a deterministic fallback key
        /// from stable inputs (userId + planCode). A genuine re-subscribe after cancellation is a new
        /// logical attempt with a new token, so it is not blocked.
        /// </summary>
        public string? IdempotencyToken { get; init; }
    }

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
                    logger.LogError(ex, "Stripe customer creation failed for user {UserId} (subscribe flow)", user.Id);
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.PlanCode), BusinessErrorMessage.PaymentGatewayUnavailable));
                }
                user.AssignStripeCustomerId(stripeCustomerId);
                logger.LogInformation(
                    "Created Stripe customer {StripeCustomerId} for user {UserId} (subscribe flow)",
                    stripeCustomerId, user.Id);
            }

            if (command.PaymentMethodConfirmed)
            {
                // The attempt id must be deterministic, never a per-call Guid: two concurrent/retried
                // confirms that share it hit the same Stripe idempotency key, so Stripe replays the one
                // subscription instead of creating a second billable one. A re-subscribe after
                // cancellation carries a new token, so it is correctly a new subscription.
                var attemptId = DeriveStripeAttemptId(command.IdempotencyToken, user.Id, plan.Code);
                SubscriptionResult subscription;
                try
                {
                    subscription = await stripeClient.CreateSubscriptionAsync(
                        stripeCustomerId, plan.StripePriceId, plan.TrialPeriodDays, attemptId, cancellationToken);
                }
                catch (StripeException ex)
                {
                    logger.LogError(ex, "Stripe subscription creation failed for user {UserId}, plan {PlanCode}", user.Id, plan.Code);
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.PlanCode), BusinessErrorMessage.PaymentGatewayUnavailable));
                }

                // Reconcile-on-retry: a prior confirm that reached Stripe but failed to commit the local
                // row leaves the subscription billed with no mirror. The deterministic attempt id makes
                // Stripe REPLAY the same SubscriptionId on retry, so resolve to any row already tracking it
                // rather than minting a second mirror for one billable subscription.
                var existingForSubscription = await userMembershipRepository.GetByStripeSubscriptionIdAsync(
                    subscription.SubscriptionId, cancellationToken);
                if (existingForSubscription != null)
                {
                    logger.LogInformation(
                        "Reconciled retried confirm for user {UserId}, plan {PlanCode} to existing membership {MembershipId} (Stripe sub {SubscriptionId})",
                        user.Id, plan.Code, existingForSubscription.Id, subscription.SubscriptionId);
                    return BusinessResult.Success(new Response(
                        MembershipId: existingForSubscription.Id,
                        SetupIntentClientSecret: string.Empty,
                        StripeCustomerId: stripeCustomerId,
                        EphemeralKey: string.Empty));
                }

                // The pre-Stripe active-membership guard ran before this Stripe call, so re-check now:
                // a concurrent confirm that won the race has since committed its row, and the loser must
                // resolve to a deterministic MembershipAlreadyActive rather than add a duplicate.
                var concurrentWinner = await userMembershipRepository.GetActiveForUserAsync(user.Id, cancellationToken);
                if (concurrentWinner != null)
                {
                    logger.LogInformation(
                        "Concurrent confirm collapsed for user {UserId}, plan {PlanCode}: resolved to existing membership {MembershipId} (Stripe sub {SubscriptionId})",
                        user.Id, plan.Code, concurrentWinner.Id, concurrentWinner.StripeSubscriptionId);
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(UserMembership), BusinessErrorMessage.MembershipAlreadyActive));
                }

                var membership = UserMembership.Create(
                    userId: user.Id,
                    membershipPlanId: plan.Id,
                    stripeSubscriptionId: subscription.SubscriptionId,
                    currentPeriodStart: subscription.CurrentPeriodStart,
                    currentPeriodEnd: subscription.CurrentPeriodEnd);
                userMembershipRepository.Add(membership);

                // The re-check above still leaves a window where the loser sees null because the winner
                // has not yet committed. Flush the insert here and own the unique-index collision: a
                // unique violation means the winner committed first, so resolve to its row and return the
                // same MembershipAlreadyActive instead of letting a raw DbUpdateException reach the
                // pipeline commit as a 500. On success the entity is Unchanged, so the pipeline's later
                // commit is a safe no-op. Exactly one row survives the race.
                try
                {
                    await userMembershipRepository.CommitAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    var winner = await userMembershipRepository.GetByStripeSubscriptionIdAsync(
                        subscription.SubscriptionId, cancellationToken);
                    logger.LogInformation(
                        "Concurrent confirm collapsed at insert for user {UserId}, plan {PlanCode}: unique-violation on Stripe sub {SubscriptionId} resolved to winning membership {MembershipId}",
                        user.Id, plan.Code, subscription.SubscriptionId, winner?.Id);
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(UserMembership), BusinessErrorMessage.MembershipAlreadyActive));
                }

                logger.LogInformation(
                    "Created UserMembership {MembershipId} (Stripe sub {SubscriptionId}) for user {UserId}, plan {PlanCode}",
                    membership.Id, subscription.SubscriptionId, user.Id, plan.Code);

                return BusinessResult.Success(new Response(
                    MembershipId: membership.Id,
                    SetupIntentClientSecret: string.Empty,
                    StripeCustomerId: stripeCustomerId,
                    EphemeralKey: string.Empty));
            }

            SetupIntentResult setupIntent;
            string ephemeralKey;
            try
            {
                setupIntent = await stripeClient.CreateSetupIntentAsync(stripeCustomerId, cancellationToken);
                ephemeralKey = await stripeClient.CreateEphemeralKeyAsync(stripeCustomerId, cancellationToken);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Stripe setup-intent / ephemeral-key creation failed for user {UserId}", user.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.PlanCode), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            return BusinessResult.Success(new Response(
                MembershipId: string.Empty,
                SetupIntentClientSecret: setupIntent.ClientSecret,
                StripeCustomerId: stripeCustomerId,
                EphemeralKey: ephemeralKey));
        }

        /// <summary>
        /// Derive the Stripe idempotency attempt id for a confirmed subscribe. The client-supplied
        /// <paramref name="idempotencyToken"/> is the authoritative collapse point — the SAME token on a
        /// retried/double-tapped confirm yields the SAME attempt id, so Stripe replays the same
        /// subscription. When the token is null/empty (web / not-yet-updated callers), fall back to a
        /// DETERMINISTIC key from stable inputs so even those callers collapse a concurrent double-tap
        /// rather than minting two subscriptions. Never a per-call Guid.
        /// </summary>
        private static string DeriveStripeAttemptId(string? idempotencyToken, string userId, string planCode)
            => string.IsNullOrWhiteSpace(idempotencyToken)
                ? $"u-{userId}-p-{planCode}"
                : $"tok-{idempotencyToken}";

        /// <summary>
        /// True when the <see cref="DbUpdateException"/> was caused by a Postgres unique-constraint
        /// violation (SQLSTATE 23505) — the StripeSubscriptionId unique index rejecting a concurrent
        /// loser's insert. Detected provider-agnostically by duck-typing the inner exception's public
        /// <c>SqlState</c> property: the AppServices layer deliberately carries no hard Npgsql reference,
        /// so we read Npgsql's <c>PostgresException.SqlState</c> reflectively rather than type-binding it.
        /// Walks the whole inner chain because EF may wrap the provider exception more than one level deep.
        /// </summary>
        private static bool IsUniqueViolation(DbUpdateException exception)
        {
            const string UniqueViolation = "23505";
            for (Exception? inner = exception.InnerException; inner is not null; inner = inner.InnerException)
            {
                var sqlState = inner.GetType()
                    .GetProperty("SqlState")?
                    .GetValue(inner) as string;
                if (sqlState == UniqueViolation)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
