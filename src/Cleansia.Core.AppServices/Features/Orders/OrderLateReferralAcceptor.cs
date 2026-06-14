using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Default <see cref="IOrderLateReferralAcceptor"/>. Wraps <see cref="IReferralRepository"/> +
/// <see cref="IReferralService"/> with the same guards and best-effort logged-and-swallowed semantics
/// the handler had inline — extracted verbatim, no behavior change.
/// </summary>
public sealed class OrderLateReferralAcceptor(
    IReferralService referralService,
    IReferralRepository referralRepository,
    ILogger<OrderLateReferralAcceptor> logger) : IOrderLateReferralAcceptor
{
    public async Task AcceptIfPresentAsync(
        string? referralCode, string userId, CancellationToken cancellationToken)
    {
        // A returning user who got the link post-signup re-types it on the
        // booking screen. Single attempt, logged on failure, never blocks the
        // booking.
        if (string.IsNullOrWhiteSpace(referralCode) || string.IsNullOrEmpty(userId))
        {
            return;
        }

        var existingReferral = await referralRepository.GetByReferredUserIdAsync(
            userId, cancellationToken);
        if (existingReferral != null)
        {
            return;
        }

        try
        {
            var acceptResult = await referralService.AcceptAsync(
                referralCode, userId, cancellationToken);
            if (!acceptResult.IsAccepted)
            {
                logger.LogInformation(
                    "Late referral accept rejected for user {UserId}, code {Code}: {Error}",
                    userId, referralCode, acceptResult.Error);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed late-accept referral code {Code} for user {UserId}",
                referralCode, userId);
        }
    }
}
