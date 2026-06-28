namespace Cleansia.Core.AppServices.Authentication;

/// <summary>
/// The booking channel the current API host serves. Each customer-facing host registers its own
/// implementation in DI (mirroring <see cref="IHostAudienceProvider"/>): the web host registers
/// <see cref="OrderChannel.Web"/>, the mobile host <see cref="OrderChannel.Mobile"/>. This is the
/// channel discriminator that lets a single shared handler/dispatcher pick exactly one Stripe charge
/// surface per card order — the web Checkout Session OR the mobile PaymentSheet PaymentIntent, never
/// both. The two customer hosts share one JWT audience, so the audience cannot tell them apart; the
/// per-host registration here is the channel signal.
/// </summary>
public interface IOrderChannelProvider
{
    OrderChannel Channel { get; }
}

public enum OrderChannel
{
    Web = 0,
    Mobile = 1,
}

public class OrderChannelProvider(OrderChannel channel) : IOrderChannelProvider
{
    public OrderChannel Channel { get; } = channel;
}
