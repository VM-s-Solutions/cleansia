namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Constants governing the referral programme. Sits next to
/// <see cref="BookingPolicy"/> so all booking-time / loyalty-touchpoint policy
/// numbers live in one folder. The credit each side earns is a money figure, so
/// it is authored per currency (<c>Currency.ReferralCredit</c>), not here.
/// </summary>
public static class ReferralPolicy
{
    /// <summary>
    /// Days from <c>Referral.AcceptedOn</c> within which the referred
    /// customer must complete their first order to qualify the referral.
    /// After this window the referral is marked Expired and no credit is
    /// granted.
    /// </summary>
    public const int QualifyingWindowDays = 90;
}
