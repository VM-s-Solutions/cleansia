using Cleansia.Core.Domain.LiveActivities;

namespace Cleansia.Core.Domain.Repositories;

public interface ILiveActivityTokenRepository : IRepository<LiveActivityToken, string>
{
    /// <summary>
    /// The single row uniquely keyed by (userId, deviceId, orderId) — the upsert lookup and the
    /// user-dismissed unregister lookup. <paramref name="orderId"/> is null for the push-to-start token.
    /// </summary>
    Task<LiveActivityToken?> GetByUserDeviceOrderAsync(string userId, string deviceId, string? orderId, CancellationToken cancellationToken);

    /// <summary>
    /// The producer gate: does this user hold any token that can drive this order's activity — a
    /// per-order update token (<c>OrderId == orderId</c>) OR the per-install push-to-start token
    /// (<c>OrderId == null</c>, which can remote-start any of the user's orders)? A transition for a
    /// user with no iOS activity registration produces nothing.
    /// </summary>
    Task<bool> HasTokensForOrderAsync(string userId, string orderId, CancellationToken cancellationToken);

    /// <summary>Every row a device holds (push-to-start included) — the logout/revoke cascade source.</summary>
    Task<IReadOnlyList<LiveActivityToken>> GetByUserAndDeviceAsync(string userId, string deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// The per-order UPDATE/END tokens (<c>OrderId == orderId</c>) — the dispatch target for
    /// <c>update</c>/<c>end</c> events, and the rows deleted after a successful terminal send.
    /// </summary>
    Task<IReadOnlyList<LiveActivityToken>> GetByUserAndOrderAsync(string userId, string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// The per-install push-to-START tokens (<c>OrderId == null</c>) — the dispatch target for a
    /// remote-<c>start</c> event (iOS 17.2+); these survive the order (never deleted on terminal).
    /// </summary>
    Task<IReadOnlyList<LiveActivityToken>> GetPushToStartTokensAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The 24h stale-activity janitor source (ADR-0029 D3): order-scoped rows (<c>OrderId != null</c>)
    /// last updated before <paramref name="cutoff"/> — the orphaned-row backstop for the lost-terminal
    /// residual. Cross-tenant (the timer has no JWT), so the tenant filter is bypassed; push-to-start
    /// rows (<c>OrderId == null</c>) are never reclaimed here.
    /// </summary>
    Task<IReadOnlyList<LiveActivityToken>> GetStaleOrderScopedTokensAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>
    /// GDPR erasure. Deletes every activity registration the subject holds — order-scoped AND per-install
    /// push-to-start. Id-keyed and set-based because the row carries no navigation to a user (ADR-0029 D3
    /// kept this off the <c>Device</c> aggregate on purpose), the shape
    /// <see cref="IEmployeePayoutDetailsRepository.RemoveForEmployeeAsync"/> already uses.
    ///
    /// <para>The row is the same fact as the <c>Device</c> row the erasure already deletes: this subject's
    /// handset, and a live APNs address for it. It is keyed on <c>UserId</c>, so after the erasure that id
    /// resolves to an anonymized user and the row's only remaining meaning is that a deleted person's
    /// handset can still be pushed to.</para>
    ///
    /// <para><b>The push-to-start row is why this cannot be left to the sweeps.</b> The 24h janitor takes
    /// order-scoped rows only, and the logout/revoke cascade needs a session the erasure has just ended —
    /// so a row with <c>OrderId == null</c> has no reaper at all and its retention is unbounded by
    /// construction. Hard delete matches this entity's own lifecycle; it is deliberately not an ADR-0007
    /// soft-delete surface.</para>
    /// </summary>
    Task RemoveForSubjectAsync(string userId, CancellationToken cancellationToken);
}
