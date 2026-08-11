using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The trial this user will actually receive on this plan. <see cref="Days"/> is what goes to Stripe —
/// 0 means "subscribe with no trial". <see cref="AlreadyUsed"/> is the per-user fact behind it, kept
/// separate so a surface can tell "you have had your free trial" from "this plan has no trial", which
/// are the same zero.
/// </summary>
public sealed record MembershipTrial(int Days, bool AlreadyUsed);

/// <summary>
/// The one place the once-per-customer trial rule is decided (owner ruling 2026-08-03: "it has to be
/// 1 trial period per customer").
///
/// <para>Enforced by us regardless of whether Stripe's "limit trial to one per customer" setting is on
/// — that setting is invisible from the code, so a rule that depends on it is a rule nobody can verify.
/// Both subscribe surfaces resolve through here BEFORE calling Stripe: mobile's confirmed subscribe and
/// the web's hosted Checkout reach Stripe by different routes, and a gate on one of the two is not a
/// gate.</para>
///
/// <para>A pure read — it never writes and never consumes. The marker is written by the two paths that
/// mirror a Stripe subscription onto a <see cref="UserMembership"/> row.</para>
/// </summary>
public interface IMembershipTrialResolver
{
    Task<MembershipTrial> ResolveForUserAsync(
        string userId,
        MembershipPlan plan,
        CancellationToken cancellationToken);
}
