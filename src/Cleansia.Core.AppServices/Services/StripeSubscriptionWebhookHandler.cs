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
/// Subscription-lifecycle half of the Stripe webhook, kept separate from the order-payment path so both
/// stay readable; they share only the entry point.
///
/// <para><b>Fail-soft throughout</b> — unknown subscriptions, unknown users and missing metadata log a
/// warning and no-op, so a retried webhook never 500s. → /flows/loyalty-and-memberships</para>
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
        var (subscriptionId, stripeStatus, periodStart, periodEnd, trialEnd) = ExtractSubscriptionShape(stripeEvent);

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
                stripeEvent, subscriptionId, periodStart, periodEnd, trialEnd, cancellationToken);
            if (membership == null)
            {
                return subscriptionId;
            }
        }

        // For invoice.payment_failed we don't have fresh period bounds —
        // pass the existing ones so the row's CurrentPeriod* stays as-is.
        // trial_end gets the same treatment inside UpdateFromStripeWebhook (ADR-0035 AM-18).
        var startToWrite = periodStart == default ? membership.CurrentPeriodStart : periodStart;
        var endToWrite = periodEnd == default ? membership.CurrentPeriodEnd : periodEnd;

        membership.UpdateFromStripeWebhook(stripeStatus, startToWrite, endToWrite, trialEnd);

        logger.LogInformation(
            "Synced membership {MembershipId} (sub {SubscriptionId}) from {EventType}: status now {Status}",
            membership.Id, subscriptionId, stripeEvent.Type, membership.Status);

        return subscriptionId;
    }

    private static (string? subscriptionId, string status, DateTime periodStart, DateTime periodEnd, DateTime? trialEnd)
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
                default,
                null);
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
            firstItem?.CurrentPeriodEnd ?? DateTime.UtcNow,
            subscription?.TrialEnd);
    }

    private async Task<UserMembership?> ProvisionFromCreatedEventAsync(
        Event stripeEvent,
        string subscriptionId,
        DateTime periodStart,
        DateTime periodEnd,
        DateTime? trialEnd,
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
            currentPeriodEnd: periodEnd,
            trialEndsAtUtc: trialEnd);
        userMembershipRepository.Add(membership);

        // The read above is a fast path, not the guarantee: two webhooks can both pass it before either
        // commits, and the filtered unique index rejects the loser with a 23505. CRITICAL — this handler
        // does NOT own its commit, so letting the violation reach the pipeline's commit surfaces a 500,
        // which makes STRIPE RETRY the webhook and amplifies rather than fixes. So flush HERE and own the
        // failure: a 23505 means a concurrent winner exists — resolve to it and return a clean no-op.
        // -> /flows/cross-cutting
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
