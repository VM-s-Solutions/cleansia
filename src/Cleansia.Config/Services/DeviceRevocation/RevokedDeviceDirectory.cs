using System.Collections.Immutable;

namespace Cleansia.Config.Services.DeviceRevocation;

/// <summary>
/// The default in-memory <see cref="IRevokedDeviceDirectory"/> (ADR-0026 D2). Holds an immutable
/// snapshot behind a single volatile reference; <see cref="Replace"/> swaps the whole reference
/// atomically so the request path never takes a lock and never reads the clock. When a poll fails the
/// refresher simply does not call <see cref="Replace"/> — the last snapshot keeps serving (fail-open,
/// ADR-0026 D4); <see cref="LastPolledAt"/> is the staleness signal the refresher warns on.
/// </summary>
public sealed class RevokedDeviceDirectory : IRevokedDeviceDirectory
{
    private volatile Snapshot _snapshot = Snapshot.Empty;

    public bool IsRevoked(string userId, string deviceId, DateTimeOffset? tokenIssuedAt)
    {
        var snapshot = _snapshot;
        if (!snapshot.Entries.TryGetValue((userId, deviceId), out var revokedAt))
        {
            return false;
        }

        // A device-claimed token whose iat can't be read cannot prove it postdates the revocation (A2).
        // Otherwise: revoke is a session kill, not a device ban - a re-login after the revoke passes.
        return tokenIssuedAt is null || tokenIssuedAt.Value < revokedAt;
    }

    public void Replace(IReadOnlyCollection<RevokedDeviceEntry> entries, DateTimeOffset polledAt)
    {
        var builder = ImmutableDictionary.CreateBuilder<(string UserId, string DeviceId), DateTimeOffset>();
        foreach (var entry in entries)
        {
            var key = (entry.UserId, entry.DeviceId);
            // A device may hold more than one deactivation row within the horizon (revoke then logout,
            // or a re-registered-then-revoked cycle). Keep the LATEST revocation instant so a later
            // revoke is never shadowed by an earlier one (a session minted between them would escape).
            if (!builder.TryGetValue(key, out var existing) || entry.RevokedAt > existing)
            {
                builder[key] = entry.RevokedAt;
            }
        }

        _snapshot = new Snapshot(builder.ToImmutable(), polledAt);
    }

    /// <summary>The instant of the last successful poll, or null before the first fill.</summary>
    public DateTimeOffset? LastPolledAt => _snapshot.PolledAt;

    private sealed record Snapshot(ImmutableDictionary<(string, string), DateTimeOffset> Entries, DateTimeOffset? PolledAt)
    {
        public static readonly Snapshot Empty = new(ImmutableDictionary<(string, string), DateTimeOffset>.Empty, null);
    }
}
