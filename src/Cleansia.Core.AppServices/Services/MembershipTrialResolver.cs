using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="IMembershipTrialResolver"/>
public sealed class MembershipTrialResolver(IUserMembershipRepository userMembershipRepository)
    : IMembershipTrialResolver
{
    public async Task<MembershipTrial> ResolveForUserAsync(
        string userId,
        MembershipPlan plan,
        CancellationToken cancellationToken)
    {
        var alreadyUsed = await userMembershipRepository.HasEverStartedTrialAsync(userId, cancellationToken);

        return new MembershipTrial(
            Days: alreadyUsed ? 0 : Math.Max(0, plan.TrialPeriodDays),
            AlreadyUsed: alreadyUsed);
    }
}
