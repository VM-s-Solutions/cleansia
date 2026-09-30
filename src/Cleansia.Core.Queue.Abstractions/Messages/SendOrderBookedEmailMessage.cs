namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// The booking e-mail with the confirmation of the contract as a PDF, sent when the contract is
/// concluded: a cash booking when it is made, a card booking once its payment completes. Dual-read off
/// the send-email queue by <see cref="MessageType"/>; <see cref="LanguageCode"/> is the fallback when the
/// order records no language of its own. <see cref="ContractConcludedOn"/> is null on a message enqueued
/// before it existed, which reads the order's creation instead.
/// </summary>
public record SendOrderBookedEmailMessage(
    string OrderId,
    string LanguageCode,
    string? TenantId,
    DateTimeOffset? ContractConcludedOn = null)
{
    public const string Discriminator = "order-booked";

    public string MessageType => Discriminator;
}
