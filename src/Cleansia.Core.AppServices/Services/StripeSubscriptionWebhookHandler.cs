using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
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

        var membership = UserMembership.Create(
            userId: userId,
            membershipPlanId: plan.Id,
            stripeSubscriptionId: subscriptionId,
            currentPeriodStart: periodStart,
            currentPeriodEnd: periodEnd);
        userMembershipRepository.Add(membership);

        logger.LogInformation(
            "Provisioned UserMembership {MembershipId} for user {UserId} from subscription.created webhook (sub {SubscriptionId})",
            membership.Id, userId, subscriptionId);

        return membership;
    }
}
