using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.HostTests.Infrastructure;

/// <summary>
/// An <see cref="IStripeClient"/> that records the membership calls a host makes instead of calling
/// Stripe: every Customer and subscription it mints is numbered, so a test can assert WHICH Customer a
/// subscription was created on. Everything a membership subscribe does not touch throws, so a test
/// that strays onto an order-payment path fails loudly rather than passing on a silent stub.
/// </summary>
public sealed class RecordingStripeClient : IStripeClient
{
    private int _customers;
    private int _subscriptions;

    public List<(string UserId, string StripeCustomerId)> CreatedCustomers { get; } = [];
    public List<(string StripeCustomerId, string StripePriceId, string SubscriptionId)> CreatedSubscriptions { get; } = [];

    public Task<string> CreateCustomerAsync(string userId, string email, string fullName, string? phone, CancellationToken cancellationToken)
    {
        var id = $"cus_fake_{++_customers}";
        CreatedCustomers.Add((userId, id));
        return Task.FromResult(id);
    }

    public Task<SubscriptionResult> CreateSubscriptionAsync(string stripeCustomerId, string stripePriceId, int trialPeriodDays, string idempotencyAttemptId, CancellationToken cancellationToken)
    {
        var id = $"sub_fake_{++_subscriptions}";
        CreatedSubscriptions.Add((stripeCustomerId, stripePriceId, id));
        return Task.FromResult(new SubscriptionResult(id, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));
    }

    public Task<string> CreateMembershipCheckoutSessionAsync(string stripeCustomerId, string stripePriceId, string userId, string membershipPlanCode, int trialPeriodDays, string idempotencyAttemptId, CancellationToken cancellationToken)
        => Task.FromResult($"https://checkout.stripe.test/{stripeCustomerId}/{stripePriceId}");

    public Task<SetupIntentResult> CreateSetupIntentAsync(string stripeCustomerId, CancellationToken cancellationToken)
        => Task.FromResult(new SetupIntentResult($"seti_{stripeCustomerId}", $"seti_secret_{stripeCustomerId}"));

    public Task<string> CreateEphemeralKeyAsync(string stripeCustomerId, CancellationToken cancellationToken)
        => Task.FromResult($"ek_{stripeCustomerId}");

    public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task RefundCheckoutSessionAsync(string stripeSessionId, decimal amount, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task RefundPaymentIntentAsync(string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<PaymentIntentResult> CreatePaymentIntentAsync(decimal amount, string currency, string stripeCustomerId, string orderId, string displayOrderNumber, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<StripePaymentSnapshot> GetPaymentSnapshotAsync(string? stripeSessionId, string? stripePaymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<SubscriptionResult> SwapSubscriptionPriceAsync(string stripeSubscriptionId, string newStripePriceId, string idempotencyAttemptId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
