namespace Cleansia.Config.Services.DeviceRevocation;

/// <summary>
/// The in-memory revoked-device snapshot (ADR-0026 D2). The request path asks
/// <see cref="IsRevoked"/> — a pure O(1) dictionary lookup, zero I/O — and the background refresher
/// feeds it via <see cref="Replace"/>. The seam is deliberately swappable: a literal-zero escalation
/// swaps in a read-through implementation behind this interface without touching hosts or claims.
/// </summary>
public interface IRevokedDeviceDirectory
{
    /// <summary>
    /// True iff a revocation entry exists for <c>(userId, deviceId)</c> and the session was
    /// established at or before it — i.e. <paramref name="tokenIssuedAt"/> is null (an
    /// unprovable-age device-claimed token, A2) or strictly precedes the recorded revocation.
    /// A token minted after the revocation (re-login) passes even while the entry is present.
    /// </summary>
    bool IsRevoked(string userId, string deviceId, DateTimeOffset? tokenIssuedAt);

    /// <summary>Atomically replaces the served snapshot (called only by the refresher on a good poll).</summary>
    void Replace(IReadOnlyCollection<RevokedDeviceEntry> entries, DateTimeOffset polledAt);
}

/// <summary>One revoked-device row as the directory keys it: the composite key plus the revocation instant.</summary>
public sealed record RevokedDeviceEntry(string UserId, string DeviceId, DateTimeOffset RevokedAt);
