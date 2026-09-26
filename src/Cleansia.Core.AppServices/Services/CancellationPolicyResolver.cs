using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// The standard <see cref="BookingPolicy"/> figures, adjusted for an ENTITLED (paid, current) Plus
/// membership: the Plus oops window always, and the plan's free-cancellation hours when it sets any.
/// Read live on every call — a membership that lapsed since the preview is judged as it stands now.
/// </summary>
public class CancellationPolicyResolver(IUserMembershipRepository userMembershipRepository)
    : ICancellationPolicyResolver
{
    public async Task<CancellationPolicy> ResolveForUserAsync(
        string? userId,
        CancellationToken cancellationToken)
    {
        var standardPolicy = new CancellationPolicy(
            FreeCancellationHours: BookingPolicy.FreeCancellationHours,
            PartialCancellationHours: BookingPolicy.PartialCancellationHours,
            PartialCancellationFeeRate: BookingPolicy.PartialCancellationFeeRate,
            LastMinuteCancellationFeeRate: BookingPolicy.LastMinuteCancellationFeeRate,
            OopsWindowMinutes: BookingPolicy.OopsWindowMinutesStandard);

        if (string.IsNullOrEmpty(userId))
        {
            return standardPolicy;
        }

        var entitledMembership = await userMembershipRepository
            .GetEntitledForUserNoTrackingAsync(userId, cancellationToken);

        if (entitledMembership == null)
        {
            return standardPolicy;
        }

        var planFreeHours = entitledMembership.MembershipPlan.FreeCancellationWindowHours;
        return standardPolicy with
        {
            OopsWindowMinutes = BookingPolicy.OopsWindowMinutesPlus,
            FreeCancellationHours = planFreeHours > 0 ? planFreeHours : standardPolicy.FreeCancellationHours,
        };
    }
}
