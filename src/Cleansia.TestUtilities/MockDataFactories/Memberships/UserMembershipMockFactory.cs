using Cleansia.Core.Domain.Memberships;

namespace Cleansia.TestUtilities.MockDataFactories.Memberships;

/// <summary>
/// A live, PAID Cleansia Plus membership — a state the entitlement predicate
/// (<c>IUserMembershipRepository.GetEntitledForUser*</c>) answers with.
///
/// <para>This exists because the recurring materialization sweep requires an entitled membership, and
/// four unrelated test classes — tenant stamping, dedupe, per-template isolation and preferred-cleaner
/// carry-through — arrange a recurring template without caring about membership at all. Each of them
/// needs the owner to be entitled or the sweep correctly does nothing and their real subject never
/// runs. One factory rather than four near-identical local helpers.</para>
///
/// <para><b>Trial-free by construction.</b> <c>trialEndsAtUtc</c> is null, so the row is past any trial
/// and a paid period — the one shape every caller of this factory wants. A test that wants the
/// UNENTITLED case should stub the repository to return null.</para>
/// </summary>
public static class UserMembershipMockFactory
{
    /// <summary>
    /// A membership that started ten days ago and runs for another twenty — comfortably inside the
    /// period on both sides, so a test's own clock cannot accidentally land on the boundary.
    /// </summary>
    public static UserMembership Paid(
        string userId,
        string? planId = null,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Cleansia Plus (Monthly)",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true);

        var membership = UserMembership.Create(
            userId,
            planId ?? plan.Id,
            MembershipPricingMockFactory.CzkCurrencyId,
            $"sub_test_{userId}",
            now.AddDays(-10),
            now.AddDays(20),
            trialEndsAtUtc: null);

        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(membership, [plan]);
        typeof(UserMembership).GetProperty(nameof(UserMembership.Currency))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(membership, [MembershipPricingMockFactory.Czk()]);

        return membership;
    }
}
