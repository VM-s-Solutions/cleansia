using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin.DTOs;

/// <summary>
/// Admin row shape for the all-referrals table — exposes both party emails
/// (full email rather than first-name only as in the customer-facing list)
/// for ops/support diagnostics.
/// </summary>
public record AdminReferralListItem(
    string Id,
    string ReferrerUserId,
    string? ReferrerEmail,
    string ReferredUserId,
    string? ReferredEmail,
    ReferralStatus Status,
    DateTimeOffset AcceptedOn,
    DateTimeOffset? FirstQualifyingOrderOn,
    int? PointsAwardedToReferrer,
    int? PointsAwardedToReferred);
