namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// The e-mail a customer gets, with an account or as a guest, when a cleaner leaves their booking and it is
/// offered to cleaners again. One per release, named by the assignment that ended. Dual-read off the
/// send-email queue by <see cref="MessageType"/>; an order no longer New or Confirmed, or no longer short of
/// the cleaners it needs, when it is read is not sent, and <see cref="LanguageCode"/> is the fallback when
/// neither the order nor the account records a language.
/// </summary>
public record SendOrderCleanerReleasedEmailMessage(string OrderId, string AssignmentId, string LanguageCode, string? TenantId)
{
    public const string Discriminator = "order-cleaner-released";

    public string MessageType => Discriminator;
}
