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
    /// window.</b> Owner ruling 2026-08-03 (ADR-0035 E-2 / ADR-0036 <c>Q-PLUS-05</c>): cut every
    /// benefit on the first payment failure. This matches <see cref="UserMembership.IsActive"/> and
    /// <c>UserMembershipRepository.ActiveForUserQuery</c>, which both require
    /// <see cref="Active"/> — so a PastDue member loses the discount, the wider cancellation window,
    /// the express waiver and the preferred-cleaner perk the instant the dunning webhook lands.
    /// <para>
    /// This comment previously claimed benefits continued during a grace window. No code ever
    /// implemented that; the owner has now ruled the opposite, knowingly accepting that a customer
    /// whose card merely expired loses benefits before they are told.
    /// </para>
    /// </summary>
    PastDue = 2,

    /// <summary>Subscription has been cancelled and is no longer providing benefits.</summary>
    Cancelled = 3,

    /// <summary>Subscription temporarily paused (e.g. user travel). Benefits do NOT apply during pause.</summary>
    Paused = 4,
}
