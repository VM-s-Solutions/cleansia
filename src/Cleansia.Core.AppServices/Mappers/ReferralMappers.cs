using Cleansia.Core.AppServices.Features.Referrals.Admin.DTOs;
using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Mappers;

public static class ReferralMappers
{
    public static AdminReferralListItem MapToAdminListItem(this Referral referral)
    {
        return new AdminReferralListItem(
            Id: referral.Id,
            ReferrerUserId: referral.ReferrerUserId,
            ReferrerEmail: referral.Referrer != null ? referral.Referrer.Email : null,
            ReferredUserId: referral.ReferredUserId,
            ReferredEmail: referral.Referred != null ? referral.Referred.Email : null,
            Status: referral.Status,
            AcceptedOn: referral.AcceptedOn,
            FirstQualifyingOrderOn: referral.FirstQualifyingOrderOn,
            PointsAwardedToReferrer: referral.PointsAwardedToReferrer,
            PointsAwardedToReferred: referral.PointsAwardedToReferred);
    }
}
