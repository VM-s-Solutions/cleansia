namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// Which of the two subscribe surfaces the act came through: the web's Stripe-hosted Checkout, or the
/// native SetupIntent/PaymentSheet flow.
/// </summary>
public enum MembershipSubscribeChannel
{
    Checkout = 1,
    Subscribe = 2,
}
