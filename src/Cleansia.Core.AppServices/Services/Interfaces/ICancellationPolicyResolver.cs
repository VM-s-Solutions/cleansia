namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Resolves the cancellation policy that should apply to a given customer.
/// Plus members get a wider free-cancellation window than non-members; this
/// service returns the right window so callers (CancelOrder handler, future
/// cancellation-preview UI) don't have to know about Plus directly.
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
/// Snapshot of the cancellation rules that apply to a given customer at a
/// given point in time. Keeps the partial-fee window and rates fixed (Plus
/// only widens the free window, doesn't change the rest).
/// </summary>
public record CancellationPolicy(
    int FreeCancellationHours,
    int PartialCancellationHours,
    decimal PartialCancellationFeeRate,
    decimal LastMinuteCancellationFeeRate);
