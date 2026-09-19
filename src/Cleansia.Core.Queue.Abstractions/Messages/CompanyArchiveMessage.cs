namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// One build of a frozen company's archive bundle (ADR-0064 D3). <see cref="RequestedOn"/> is the
/// freeze instant the bundle folder is named by; the consumer checks it against the row, so a
/// message from a request the row no longer carries is discarded rather than built into the wrong
/// folder.
/// </summary>
public record CompanyArchiveMessage(string TenantId, DateTimeOffset RequestedOn);
