namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Resolves the cancellation policy that applies to a given customer, so callers (the cancel paths,
/// both previews, the booking evidence) don't have to know about Plus directly.
/// </summary>
public interface ICancellationPolicyResolver
{
    /// <summary>
    /// Resolve the policy for the given user. Pass null for guest/anonymous
    /// orders — they fall through to the default non-member policy.
    /// </summary>
    Task<CancellationPolicy> ResolveForUserAsync(string? userId, CancellationToken cancellationToken);
}

/// <summary>
/// The cancellation rules that apply to one customer at one moment. An entitled Plus membership moves
/// two things, independently: the oops window after booking (<c>BookingPolicy.OopsWindowMinutesPlus</c>)
/// and, when the plan sets one, the free window before the cleaning. The partial and last-minute
/// thresholds and rates never move.
/// </summary>
public record CancellationPolicy(
    int FreeCancellationHours,
    int PartialCancellationHours,
    decimal PartialCancellationFeeRate,
    decimal LastMinuteCancellationFeeRate,
    int OopsWindowMinutes);
