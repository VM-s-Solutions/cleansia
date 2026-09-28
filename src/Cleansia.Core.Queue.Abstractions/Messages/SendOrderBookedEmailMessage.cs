namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// The informational e-mail a cash booking gets in place of a receipt, which is issued only once the
/// cleaner has recorded the cash. Dual-read off the send-email queue by <see cref="MessageType"/>;
/// <see cref="LanguageCode"/> is the fallback when the order records no language of its own.
/// </summary>
public record SendOrderBookedEmailMessage(string OrderId, string LanguageCode, string? TenantId)
{
    public const string Discriminator = "order-booked";

    public string MessageType => Discriminator;
}
