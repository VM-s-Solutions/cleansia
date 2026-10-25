namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// The e-mail a customer gets, with an account or as a guest, when the last cleaner on their booking left
/// it and the order went back from Confirmed to New: another cleaner is being looked for. One per release,
/// named by the assignment that ended. Dual-read off the send-email queue by <see cref="MessageType"/>; an
/// order no longer at New when it is read is not sent, and <see cref="LanguageCode"/> is the fallback when
/// neither the order nor the account records a language.
/// </summary>
public record SendOrderCleanerReleasedEmailMessage(string OrderId, string AssignmentId, string LanguageCode, string? TenantId)
{
    public const string Discriminator = "order-cleaner-released";

    public string MessageType => Discriminator;
}
