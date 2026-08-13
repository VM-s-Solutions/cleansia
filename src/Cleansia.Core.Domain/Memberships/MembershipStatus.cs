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

    /// <summary>
    /// Latest invoice failed; Stripe is retrying. <b>Benefits STOP immediately — there is no grace
    /// window.</b> Owner ruling: cut every benefit on the first payment failure. A past-due member loses
    /// the discount, the wider cancellation window, the express waiver and the preferred-cleaner perk the
    /// instant the dunning webhook lands — knowingly accepting that a customer whose card merely expired
    /// loses benefits before they are told. → /flows/loyalty-and-memberships
    /// </summary>
    PastDue = 2,

    /// <summary>Subscription has been cancelled and is no longer providing benefits.</summary>
    Cancelled = 3,

    /// <summary>Subscription temporarily paused (e.g. user travel). Benefits do NOT apply during pause.</summary>
    Paused = 4,
}
