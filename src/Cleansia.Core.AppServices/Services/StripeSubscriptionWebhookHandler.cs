using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// Implements <see cref="IStripeSubscriptionWebhookHandler"/>. Extracted from
/// <c>HandlePaymentNotification.Handler</c> so the order-payment and
/// subscription-lifecycle paths stay independently readable — they share
/// only the webhook entry point in the handler.
///
/// Resolves the local row by Stripe subscription id, auto-provisions on
/// <c>customer.subscription.created</c> when missing (web Checkout flow),
/// then applies the status + period bounds via <c>UpdateFromStripeWebhook</c>.
/// Fail-soft throughout — unknown subscriptions, unknown users, and missing
/// metadata log a warning and no-op so retried webhooks don't 500.
/// </summary>
public class StripeSubscriptionWebhookHandler(
    IUserRepository userRepository,
    IUserMembershipRepository userMembershipRepository,
    IMembershipPlanRepository membershipPlanRepository,
    ITenantProvider tenantProvider,
    ILogger<StripeSubscriptionWebhookHandler> logger) : IStripeSubscriptionWebhookHandler
{
    public async Task<string> HandleAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var (subscriptionId, stripeStatus, periodStart, periodEnd) = ExtractSubscriptionShape(stripeEvent);

        if (string.IsNullOrEmpty(subscriptionId))
        {
            logger.LogWarning(
                "Subscription webhook {EventType} arrived without a subscription id; ignoring",
                stripeEvent.Type);
            return string.Empty;
        }

        var membership = await userMembershipRepository
            .GetByStripeSubscriptionIdAsync(subscriptionId, cancellationToken);

        if (membership != null && !string.IsNullOrEmpty(membership.TenantId))
        {
            tenantProvider.SetTenantOverride(membership.TenantId);
        }

        if (membership == null)
        {
            membership = await ProvisionFromCreatedEventAsync(
                stripeEvent, subscriptionId, periodStart, periodEnd, cancellationToken);
            if (membership == null)
            {
                return subscriptionId;
            }
        }

        // For invoice.payment_failed we don't have fresh period bounds —
        // pass the existing ones so the row's CurrentPeriod* stays as-is.
        var startToWrite = periodStart == default ? membership.CurrentPeriodStart : periodStart;
        var endToWrite = periodEnd == default ? membership.CurrentPeriodEnd : periodEnd;

        membership.UpdateFromStripeWebhook(stripeStatus, startToWrite, endToWrite);

        logger.LogInformation(
            "Synced membership {MembershipId} (sub {SubscriptionId}) from {EventType}: status now {Status}",
            membership.Id, subscriptionId, stripeEvent.Type, membership.Status);

        return subscriptionId;
    }

    private static (string? subscriptionId, string status, DateTime periodStart, DateTime periodEnd)
        ExtractSubscriptionShape(Event stripeEvent)
    {
        if (stripeEvent.Type == Constants.StripeEventType.InvoicePaymentFailed)
        {
            var invoice = stripeEvent.Data.Object as Invoice;
            // Stripe.net 50.x: subscription id moved into Parent.SubscriptionDetails.
            // Older invoices (one-off charges, our checkout-session flow) have
            // no parent subscription → null id and we no-op upstream.
            return (
                invoice?.Parent?.SubscriptionDetails?.SubscriptionId,
                "past_due",
                default,
                default);
        }

        var subscription = stripeEvent.Data.Object as Subscription;
        // Period bounds live on each SubscriptionItem in Stripe.net 50.x.
        // We have a single Plus item per subscription, so the first item's
        // bounds are the subscription's bounds.
        var firstItem = subscription?.Items?.Data?.FirstOrDefault();
        return (
            subscription?.Id,
            subscription?.Status ?? "canceled",
            firstItem?.CurrentPeriodStart ?? DateTime.UtcNow,
            firstItem?.CurrentPeriodEnd ?? DateTime.UtcNow);
    }

    private async Task<UserMembership?> ProvisionFromCreatedEventAsync(
        Event stripeEvent,
        string subscriptionId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        // Only customer.subscription.created provisions the row (web Checkout
        // flow). Other event types arriving for an unknown subscription means
        // it was created in the Stripe Dashboard and we never tracked it —
        // safer to ignore than guess.
        if (stripeEvent.Type != Constants.StripeEventType.SubscriptionCreated)
        {
            logger.LogWarning(
                "Subscription webhook {EventType} for sub {SubscriptionId} has no local UserMembership row; ignoring",
                stripeEvent.Type, subscriptionId);
            return null;
        }

        var stripeSub = stripeEvent.Data.Object as Subscription;
        var userId = stripeSub?.Metadata?.GetValueOrDefault("UserId");
        var planCode = stripeSub?.Metadata?.GetValueOrDefault("MembershipPlanCode");
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(planCode))
        {
            logger.LogWarning(
                "subscription.created webhook for sub {SubscriptionId} missing UserId/MembershipPlanCode metadata; can't provision local row",
                subscriptionId);
            return null;
        }

        var owningUser = await userRepository.GetByIdIgnoringTenantAsync(userId, cancellationToken);
        if (owningUser == null)
        {
            logger.LogWarning(
                "subscription.created webhook references unknown user {UserId}; can't provision local row",
                userId);
            return null;
        }
        if (!string.IsNullOrEmpty(owningUser.TenantId))
        {
            tenantProvider.SetTenantOverride(owningUser.TenantId);
        }

        var plan = await membershipPlanRepository.GetByCodeAsync(planCode, cancellationToken);
        if (plan == null)
        {
            logger.LogWarning(
                "subscription.created webhook references unknown plan code {PlanCode}; can't provision local row",
                planCode);
            return null;
        }

        // SEC-W2 / ADR-0002 D2 — ASSERT BEFORE ACTING. The web Checkout flow only creates the
        // Stripe Session; this webhook is the SOLE creator of the local row, and unlike the request path
        // (CreateMembershipCheckoutSession) it never checked for an existing active membership. So a user
        // who already has one and reaches Stripe again (stale tab / Dashboard / two near-simultaneous
        // checkouts — the request-side guard only blocks session-CREATION, not Stripe-side reality) got a
        // SECOND active row → double benefits + reconciliation drift. The tenant override is set above
        // (owningUser.TenantId), so GetActiveForUserAsync resolves in the right tenant scope (S8). If an
        // active membership already exists, a duplicate provision is an idempotent no-op SUCCESS: log a
        // reconcile/skip and return the existing row WITHOUT Create/Add. The outer
        // HandlePaymentNotification handler still stamps the event processed either way.
        var existingActive = await userMembershipRepository.GetActiveForUserAsync(userId, cancellationToken);
        if (existingActive != null)
        {
            logger.LogWarning(
                "subscription.created webhook for sub {SubscriptionId} but user {UserId} already has active membership {MembershipId}; skipping duplicate provision (reconcile no-op)",
                subscriptionId, userId, existingActive.Id);
            return existingActive;
        }

        var membership = UserMembership.Create(
            userId: userId,
            membershipPlanId: plan.Id,
            stripeSubscriptionId: subscriptionId,
            currentPeriodStart: periodStart,
            currentPeriodEnd: periodEnd);
        userMembershipRepository.Add(membership);

        // SEC-W2 / S7a + S7b — CLOSE the check-then-insert race at the write boundary. The
        // GetActiveForUserAsync read above is a fast path, not the guarantee: two webhooks (or a webhook +
        // a confirmed request-path subscribe) can both pass the read before either commits, and the
        // FILTERED UNIQUE INDEX on (TenantId, UserId) WHERE Status=Active then rejects the loser with a
        // Postgres 23505. CRITICAL (S7b): this handler does NOT own its own commit — it runs inside
        // HandlePaymentNotification.Handle under the UnitOfWorkPipelineBehavior, whose CommitAsync fires
        // AFTER the handler returns. If we let the violation reach that pipeline commit it surfaces as an
        // unhandled DbUpdateException → 500, which makes STRIPE RETRY the webhook (amplifying, not fixing).
        // So FLUSH the insert HERE and own the failure: a 23505 means a concurrent winner already created
        // the active row — resolve to it and return it as a clean reconcile no-op (no throw). On success
        // the entity is Unchanged, so the pipeline's final CommitAsync is a safe no-op; on the violation we
        // returned the winner, and the pipeline commit then has nothing to persist for this row either.
        try
        {
            await userMembershipRepository.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Detach the rejected insert so the OUTER pipeline's final CommitAsync (which also carries the
            // ProcessedStripeEvent stamp Add-ed by HandlePaymentNotification before this handler ran) does
            // NOT retry the duplicate row and re-raise the same 23505. Remove() on a still-Added entity
            // detaches it from the change-tracker (nothing was persisted), leaving only the event stamp to
            // commit. The event is still marked processed — a duplicate provision is an idempotent no-op.
            userMembershipRepository.Remove(membership);

            var winner = await userMembershipRepository.GetActiveForUserAsync(userId, cancellationToken);
            logger.LogWarning(
                "subscription.created webhook for sub {SubscriptionId}, user {UserId} lost the active-membership race (unique-violation); resolved to winning membership {MembershipId} (reconcile no-op)",
                subscriptionId, userId, winner?.Id);
            return winner;
        }

        logger.LogInformation(
            "Provisioned UserMembership {MembershipId} for user {UserId} from subscription.created webhook (sub {SubscriptionId})",
            membership.Id, userId, subscriptionId);

        return membership;
    }

    /// <summary>
    /// True when the <see cref="DbUpdateException"/> was caused by a Postgres unique-constraint violation
    /// (SQLSTATE 23505) — the filtered (TenantId, UserId) WHERE Status=Active unique index (or the
    /// StripeSubscriptionId unique index) rejecting a concurrent loser's insert. Detected
    /// provider-agnostically by duck-typing the inner exception's public <c>SqlState</c> string property:
    /// the AppServices layer deliberately carries no hard Npgsql reference, so we read Npgsql's
    /// <c>PostgresException.SqlState</c> reflectively rather than type-binding it. Walks the whole inner
    /// chain because EF may wrap the provider exception more than one level deep. Mirrors
    /// <c>CreateMembershipSubscription.Handler.IsUniqueViolation</c> / <c>LoyaltyService</c>.
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
