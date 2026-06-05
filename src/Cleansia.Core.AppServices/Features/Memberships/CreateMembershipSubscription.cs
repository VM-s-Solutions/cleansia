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

namespace Cleansia.Core.AppServices.Features.Memberships;

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
                // ADR-0002 idempotent-consumer contract: the Stripe attempt id is
                // DERIVED from the client-supplied idempotency token, NOT a fresh Guid.NewGuid() per call.
                // The real Stripe key is sub-{customer}-{price}-{attemptId}, so two concurrent/retried
                // confirms carrying the SAME token hit the SAME Stripe key and Stripe REPLAYS the same
                // subscription instead of creating a second one (the double-charge hole). A genuine
                // re-subscribe after cancellation carries a NEW token => a new attempt id => a new
                // subscription, so it is not blocked. When the token is null/empty (web / not-yet-updated
                // callers), fall back to a DETERMINISTIC key from stable inputs (userId + planCode) so
                // even those callers' double-taps collapse on one Stripe key — defense in depth.
                var attemptId = DeriveStripeAttemptId(command.IdempotencyToken, user.Id, plan.Code);
                var subscription = await stripeClient.CreateSubscriptionAsync(
                    stripeCustomerId, plan.StripePriceId, plan.TrialPeriodDays, attemptId, cancellationToken);

                // Local-row collapse: with the same Stripe key both concurrent
                // confirms receive the SAME subscription.SubscriptionId. The :57 guard ran BEFORE the
                // Stripe call (a TOCTOU window), so re-check active membership now — the loser sees the
                // winner's just-created row and returns a deterministic MembershipAlreadyActive instead
                // of Add-ing a duplicate that would otherwise hit the unique index on StripeSubscriptionId
                // and surface as a raw DbUpdateException/500 at the pipeline commit. This handler is a
                // top-level command (not inside the paid-order txn), so failing only THIS request is
                // acceptable; the loser gets a clean result.
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

                // CLOSE the concurrent window at the write boundary, don't just
                // narrow it. The re-check above only narrows: in the genuine race the LOSER re-checks BEFORE the
                // WINNER's pipeline CommitAsync has made the winner's row visible, sees null, and Add-s a row that
                // collides on the StripeSubscriptionId unique index (UserMembershipEntityConfiguration:56-57). If
                // we let that collision reach the pipeline's UnitOfWorkPipelineBehavior.CommitAsync it surfaces as
                // an unhandled DbUpdateException → 500. So FLUSH the insert HERE and own the failure: a Postgres
                // 23505 unique-violation means the winner committed first — resolve to the winner's row and return
                // the same deterministic MembershipAlreadyActive the re-check would have. Once this SaveChanges
                // succeeds the entity is Unchanged, so the pipeline's final CommitAsync is a safe no-op; on the
                // violation we return a Failure (no throw), and the pipeline commit then has nothing to persist
                // for this row either. Exactly ONE UserMembership row survives; the loser NEVER sees a 500.
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

            var setupIntent = await stripeClient.CreateSetupIntentAsync(stripeCustomerId, cancellationToken);
            var ephemeralKey = await stripeClient.CreateEphemeralKeyAsync(stripeCustomerId, cancellationToken);

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
