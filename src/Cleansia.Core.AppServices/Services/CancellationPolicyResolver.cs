using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// Default implementation: looks up the user's active membership and uses its
/// FreeCancellationWindowHours when one exists, otherwise returns the standard
/// BookingPolicy values. Today (no Plus product live) every call returns the
/// standard policy. When Plus launches, members automatically pick up the
/// wider free window without any changes here.
/// </summary>
public class CancellationPolicyResolver(IUserMembershipRepository userMembershipRepository)
    : ICancellationPolicyResolver
{
    public async Task<CancellationPolicy> ResolveForUserAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var defaultPolicy = new CancellationPolicy(
            FreeCancellationHours: BookingPolicy.FreeCancellationHours,
            PartialCancellationHours: BookingPolicy.PartialCancellationHours,
            PartialCancellationFeeRate: BookingPolicy.PartialCancellationFeeRate,
            LastMinuteCancellationFeeRate: BookingPolicy.LastMinuteCancellationFeeRate);

        if (string.IsNullOrEmpty(userId))
        {
            return defaultPolicy;
        }

        var activeMembership = await userMembershipRepository
            .GetActiveForUserNoTrackingAsync(userId, cancellationToken);

        if (activeMembership == null
            || activeMembership.MembershipPlan.FreeCancellationWindowHours <= 0)
        {
            return defaultPolicy;
        }

        return defaultPolicy with
        {
            FreeCancellationHours = activeMembership.MembershipPlan.FreeCancellationWindowHours,
        };
    }
}
