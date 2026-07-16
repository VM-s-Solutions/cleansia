using System.Collections.Immutable;

namespace Cleansia.Config.Services.UserRevocation;

/// <summary>
/// The default in-memory <see cref="IRevokedUserDirectory"/> (ADR-0027 D1). Holds an immutable
/// snapshot behind a single volatile reference; <see cref="Replace"/> swaps the whole reference
/// atomically so the request path never takes a lock and never reads the clock. When a poll fails the
/// refresher simply does not call <see cref="Replace"/> — the last snapshot keeps serving (fail-open,
/// ADR-0027 D5); <see cref="LastPolledAt"/> is the staleness signal the refresher warns on.
/// </summary>
public sealed class RevokedUserDirectory : IRevokedUserDirectory
{
    private volatile Snapshot _snapshot = Snapshot.Empty;

    public bool IsRevoked(string userId, DateTimeOffset? tokenIssuedAt)
    {
        var snapshot = _snapshot;
        if (!snapshot.Entries.TryGetValue(userId, out var resetAt))
        {
            return false;
        }

        // A token whose iat can't be read cannot prove it postdates the reset (A2). Otherwise: reset is
        // a session kill, not a user ban - the post-reset re-login (iat > resetAt) passes. iat is a
        // whole-second NumericDate while resetAt is sub-second, so a re-login inside the reset's
        // wall-clock second can 401 once and self-heal via its live refresh token (ADR-0027 U1).
        return tokenIssuedAt is null || tokenIssuedAt.Value < resetAt;
    }

    public void Replace(IReadOnlyCollection<RevokedUserEntry> entries, DateTimeOffset polledAt)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, DateTimeOffset>();
        foreach (var entry in entries)
        {
            // A user may reset twice within the horizon. Keep the LATEST reset instant so a later reset
            // is never shadowed by an earlier one (a session minted between them would escape).
            if (!builder.TryGetValue(entry.UserId, out var existing) || entry.ResetAt > existing)
            {
                builder[entry.UserId] = entry.ResetAt;
            }
        }

        _snapshot = new Snapshot(builder.ToImmutable(), polledAt);
    }

    /// <summary>The instant of the last successful poll, or null before the first fill.</summary>
    public DateTimeOffset? LastPolledAt => _snapshot.PolledAt;

    private sealed record Snapshot(ImmutableDictionary<string, DateTimeOffset> Entries, DateTimeOffset? PolledAt)
    {
        public static readonly Snapshot Empty = new(ImmutableDictionary<string, DateTimeOffset>.Empty, null);
    }
}
