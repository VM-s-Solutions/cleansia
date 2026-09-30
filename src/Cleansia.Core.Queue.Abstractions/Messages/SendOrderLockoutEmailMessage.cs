namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// The e-mail a signed-in customer gets when an administrator confirms that the cleaner could not get in:
/// the booking is cancelled at the whole price. A guest is told by the guest cancellation e-mail instead.
/// Dual-read off the send-email queue by <see cref="MessageType"/>; <see cref="LanguageCode"/> is the
/// fallback when neither the order nor the account records a language.
/// </summary>
public record SendOrderLockoutEmailMessage(string OrderId, string LanguageCode, string? TenantId)
{
    public const string Discriminator = "order-lockout";

    public string MessageType => Discriminator;
}
