namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// The e-mail a customer gets when their saved card could not be charged for what they owe on an order:
/// the amount, the order and <see cref="PayUrl"/>, a Checkout Session created where the failure was
/// heard. Dual-read off the send-email queue by <see cref="MessageType"/>; a receivable settled or written
/// off before the send is not chased.
/// </summary>
public record SendReceivablePayLinkEmailMessage(string ReceivableId, int Attempt, string PayUrl, string? TenantId)
{
    public const string Discriminator = "receivable-pay-link";

    public string MessageType => Discriminator;
}
