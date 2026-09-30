namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// The cleaner's copy of a contract for work they accepted, as a PDF, one per acceptance. Dual-read off
/// the send-email queue by <see cref="MessageType"/>; the recipient and their language are read from the
/// cleaner's account when it is sent, so the body carries no address.
/// </summary>
public record SendWorkContractEmailMessage(string AcceptanceId, string? TenantId)
{
    public const string Discriminator = "work-contract";

    public string MessageType => Discriminator;
}
