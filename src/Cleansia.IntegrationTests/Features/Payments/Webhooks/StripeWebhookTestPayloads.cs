using System.Globalization;
using Stripe;

namespace Cleansia.IntegrationTests.Features.Payments.Webhooks;

/// <summary>
/// Builds the raw Stripe webhook bodies + signatures the integration suite POSTs through the real
/// MediatR pipeline. The bodies are hand-written JSON (not SDK objects) so a test can produce a body
/// the host's <c>EventUtility.ConstructEvent</c> deserializes exactly as a live Stripe delivery would,
/// while the signing helpers let a test forge / strip the <c>Stripe-Signature</c> for the AC4/AC5
/// signature-lock cases. Mirrors the signing shape proven by the Wave-0 unit suite
/// (HandleChargebackNotificationTests.SignPayload — <c>t=…,v1=HMAC</c>).
/// </summary>
internal static class StripeWebhookTestPayloads
{
    /// <summary>The webhook secret the IntegrationTests host is configured with (appsettings).</summary>
    public const string ConfiguredWebhookSecret = "whsec_your_webhook_secret";

    /// <summary>A secret the host is NOT configured with — used to forge an attacker-signed event.</summary>
    public const string WrongWebhookSecret = "whsec_attacker_minted_secret";

    public static string CheckoutSessionCompletedBody(string eventId, string orderId, string sessionId = "cs_test_session")
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "checkout.session.completed",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "payment_status": "paid",
              "status": "complete",
              "mode": "payment",
              "metadata": {
                "OrderId": "{{orderId}}"
              }
            },
            "previous_attributes": null
          }
        }
        """;
    }

    public static string SubscriptionCreatedBody(
        string eventId, string subscriptionId, string userId, string planCode)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var periodStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var periodEnd = DateTimeOffset.UtcNow.AddMonths(1).ToUnixTimeSeconds();
        return $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "customer.subscription.created",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "object": "subscription",
              "status": "active",
              "metadata": {
                "UserId": "{{userId}}",
                "MembershipPlanCode": "{{planCode}}"
              },
              "items": {
                "object": "list",
                "data": [
                  {
                    "id": "si_test_item",
                    "object": "subscription_item",
                    "current_period_start": {{periodStart}},
                    "current_period_end": {{periodEnd}}
                  }
                ]
              }
            },
            "previous_attributes": null
          }
        }
        """;
    }

    /// <summary>A valid Stripe signature header (<c>t=…,v1=HMAC</c>) for the body, signed with the secret.</summary>
    public static string Sign(string body, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = EventUtility.ComputeSignature(secret, timestamp, body);
        return $"t={timestamp},v1={signature}";
    }
}
