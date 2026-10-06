namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// The e-mail a signed-in customer gets when the cleaner reports that the cash for a finished cleaning was
/// not paid, or an administrator completes the order in progress through the status override (owner ruling
/// 2026-10-06): the price is owed, it is paid on the order's page, and no new booking is taken until then.
/// Dual-read off the send-email queue by <see cref="MessageType"/>; a receivable settled or written off
/// before the send is not chased. <see cref="LanguageCode"/> is the fallback when neither the order nor the
/// account records a language.
/// </summary>
public record SendOrderCashNotPaidEmailMessage(string ReceivableId, string LanguageCode, string? TenantId)
{
    public const string Discriminator = "order-cash-not-paid";

    public string MessageType => Discriminator;
}
