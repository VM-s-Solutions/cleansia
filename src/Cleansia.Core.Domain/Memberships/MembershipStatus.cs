using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Memberships;

/// <summary>
/// Lifecycle of a <see cref="UserMembership"/>. Mirrors the subset of Stripe
/// subscription statuses we actually act on; statuses we don't care about
/// (e.g. <c>incomplete_expired</c>, <c>trialing</c>) collapse to
/// <see cref="Cancelled"/> or <see cref="Active"/> respectively when the
/// webhook lands.
/// </summary>
[SwaggerEnumAsInt]
public enum MembershipStatus
{
    /// <summary>Subscription is paid up and benefits apply.</summary>
    Active = 1,

    /// <summary>Latest invoice failed; Stripe is retrying. Benefits still apply during the grace window.</summary>
    PastDue = 2,

    /// <summary>Subscription has been cancelled and is no longer providing benefits.</summary>
    Cancelled = 3,

    /// <summary>Subscription temporarily paused (e.g. user travel). Benefits do NOT apply during pause.</summary>
    Paused = 4,
}
