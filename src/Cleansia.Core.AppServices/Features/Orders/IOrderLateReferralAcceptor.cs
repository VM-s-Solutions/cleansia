namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Owns the late-referral-acceptance concern lifted out of <see cref="CreateOrder.Handler"/>: a
/// returning user who got the invite link after signing up re-types the code on the booking screen.
///
/// The contract preserves the handler's original semantics exactly:
///   * runs only when a referral code is present and the user is logged in;
///   * no-ops when the user already has a referral row (already referred);
///   * is <b>best-effort</b> — a rejected accept is logged at Information, a thrown error is logged at
///     Warning and swallowed; it never blocks the booking.
/// </summary>
public interface IOrderLateReferralAcceptor
{
    Task AcceptIfPresentAsync(
        string? referralCode, string userId, CancellationToken cancellationToken);
}
