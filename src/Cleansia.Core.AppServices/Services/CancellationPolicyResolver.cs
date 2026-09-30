using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// The standard <see cref="BookingPolicy"/> figures, adjusted for an ENTITLED (paid or trialing, current) Plus
/// membership — the Plus oops window always, and the plan's free-cancellation hours when it sets any —
/// or else for the customer's first booking, whose oops window is the longer one too.
/// Read live on every call — a membership that lapsed since the preview is judged as it stands now.
/// </summary>
public class CancellationPolicyResolver(
    IUserMembershipRepository userMembershipRepository,
    IOrderRepository orderRepository)
    : ICancellationPolicyResolver
{
    public async Task<CancellationPolicy> ResolveForOrderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var standardPolicy = new CancellationPolicy(
            FreeCancellationHours: BookingPolicy.FreeCancellationHours,
            PartialCancellationHours: BookingPolicy.PartialCancellationHours,
            PartialCancellationFeeRate: BookingPolicy.PartialCancellationFeeRate,
            LastMinuteCancellationFeeRate: BookingPolicy.LastMinuteCancellationFeeRate,
            OopsWindowMinutes: BookingPolicy.OopsWindowMinutesStandard,
            OopsWindowRule: OopsWindowRule.Standard);

        if (!string.IsNullOrEmpty(order.UserId))
        {
            var entitledMembership = await userMembershipRepository
                .GetEntitledForUserNoTrackingAsync(order.UserId, cancellationToken);

            if (entitledMembership != null)
            {
                var planFreeHours = entitledMembership.MembershipPlan.FreeCancellationWindowHours;
                return standardPolicy with
                {
                    OopsWindowMinutes = BookingPolicy.OopsWindowMinutesPlus,
                    OopsWindowRule = OopsWindowRule.Plus,
                    FreeCancellationHours = planFreeHours > 0 ? planFreeHours : standardPolicy.FreeCancellationHours,
                };
            }
        }

        return await orderRepository.IsFirstBookingAsync(order, cancellationToken)
            ? standardPolicy with
            {
                OopsWindowMinutes = BookingPolicy.OopsWindowMinutesFirstBooking,
                OopsWindowRule = OopsWindowRule.FirstBooking,
            }
            : standardPolicy;
    }
}
