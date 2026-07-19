using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.LiveActivities;

/// <summary>
/// Where one order's live card (<see cref="OrderId"/> set) — or a device's remote-start ability
/// (<see cref="OrderId"/> null, the per-install push-to-start token) — is addressable on APNs
/// (ADR-0029 D3). A dedicated per-order registration, never a column on <c>Device</c>: update tokens
/// are per (device × order), rotate mid-activity, and live for hours, so overloading the
/// install-lived, revocation-coupled <c>Device</c> aggregate is rejected on cardinality and lifecycle.
///
/// Rows are hours-lived operational addressing data and are HARD-deleted (consumer terminal-send
/// cleanup, APNs 410 prune, the 24h janitor, and the logout/revoke cascade) — deliberately NOT the
/// ADR-0007 soft-delete domain surface.
/// </summary>
public class LiveActivityToken : Auditable, ITenantEntity
{
    public string UserId { get; private set; } = default!;

    // The client-generated id Device rows also carry — correlation for the logout/revoke cascade,
    // deliberately NOT a foreign key (an activity token references no Device row and outlives none).
    public string DeviceId { get; private set; } = default!;

    // null = the per-install push-to-start token (remote-starts any of this user's orders, iOS 17.2+);
    // non-null = a per-activity update token for that specific order.
    public string? OrderId { get; private set; }

    public string Token { get; private set; } = default!;

    public DateTimeOffset LastUpdatedAt { get; private set; }

    private LiveActivityToken() { }

    public static LiveActivityToken Create(string userId, string deviceId, string? orderId, string token, string? tenantId)
    {
        return new LiveActivityToken
        {
            UserId = userId,
            DeviceId = deviceId,
            OrderId = orderId,
            Token = token,
            TenantId = tenantId,
            LastUpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    // ActivityKit rotates update tokens mid-activity and push-to-start tokens across installs, so
    // registration upserts on (UserId, DeviceId, OrderId) — last write wins.
    public void Refresh(string token)
    {
        Token = token;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }
}
