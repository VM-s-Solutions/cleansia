namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// One administrator e-mail on the send-email queue, dual-read by the consumer off the same queue as
/// <see cref="SendEmailMessage"/> by <see cref="MessageType"/>. It carries the event and its loc-args,
/// never rendered text and never a person beyond the address: the consumer renders the subject and
/// the body from the event's copy in <see cref="LanguageCode"/>. <see cref="Subject"/> is the event's
/// dedup subject; with the key and the hashed address it is the deterministic idempotency key the
/// consumer synthesises again.
/// </summary>
public record SendAdminNotificationEmailMessage(
    string EventKey,
    string Subject,
    IReadOnlyDictionary<string, string> Args,
    string Email,
    string LanguageCode,
    string TenantId)
{
    public const string Discriminator = "admin-notification";

    public string MessageType => Discriminator;
}
