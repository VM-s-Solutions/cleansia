namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Constants governing the referral programme. Sits next to
/// <see cref="BookingPolicy"/> so all booking-time / loyalty-touchpoint policy
/// numbers live in one folder.
/// </summary>
public static class ReferralPolicy
{
    /// <summary>
    /// Tier-points granted to BOTH the referrer and the referred customer
    /// after the referred customer's first completed order.
    /// </summary>
    public const int PointsPerSide = 150;

    /// <summary>
    /// Days from <c>Referral.AcceptedOn</c> within which the referred
    /// customer must complete their first order to trigger the +150 grant.
    /// After this window the referral is marked Expired and no points are
    /// granted.
    /// </summary>
    public const int QualifyingWindowDays = 90;
}
