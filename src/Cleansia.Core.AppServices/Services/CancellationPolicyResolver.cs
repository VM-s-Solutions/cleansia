using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// The schedule the order was booked under (its frozen <see cref="CancellationTerms"/>), adjusted for an
/// ENTITLED (paid or trialing, current) Plus membership — the Plus oops window and the order's Plus free
/// hours — or else for the customer's first booking, whose oops window is the longer one too.
/// Plus status and the first-booking test are read live on every call — a membership that lapsed since the
/// preview is judged as it stands now — but every figure is the order's.
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
            FreeCancellationHours: order.CancellationFreeHours,
            PartialCancellationHours: order.CancellationPartialHours,
            PartialCancellationFeeRate: order.CancellationPartialFeeRate,
            LastMinuteCancellationFeeRate: order.CancellationLastMinuteFeeRate,
            OopsWindowMinutes: BookingPolicy.OopsWindowMinutesStandard,
            OopsWindowRule: OopsWindowRule.Standard);

        if (!string.IsNullOrEmpty(order.UserId)
            && await userMembershipRepository.GetEntitledForUserNoTrackingAsync(order.UserId, cancellationToken) != null)
        {
            return standardPolicy with
            {
                OopsWindowMinutes = BookingPolicy.OopsWindowMinutesPlus,
                OopsWindowRule = OopsWindowRule.Plus,
                FreeCancellationHours = order.CancellationPlusFreeHours,
            };
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
