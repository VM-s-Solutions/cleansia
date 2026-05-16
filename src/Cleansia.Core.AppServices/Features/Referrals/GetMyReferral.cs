using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Referrals;

public class GetMyReferral
{
    public record Query : IQuery<Response>;

    public record Response(
        string Code,
        int TimesUsed,
        int QualifiedCount,
        int AcceptedCount,
        int PointsPerReferral);

    public class Handler(
        IReferralService referralService,
        IReferralRepository referralRepository,
        IUserSessionProvider userSessionProvider) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var code = await referralService.EnsureCodeForUserAsync(userId, cancellationToken);

            var totalCount = await referralRepository.CountByReferrerAsync(userId, cancellationToken);
            var referrals = await referralRepository.GetByReferrerAsync(
                userId, 0, totalCount > 0 ? totalCount : 0, cancellationToken);

            var qualified = referrals.Count(r => r.Status == ReferralStatus.Qualified);
            var accepted = referrals.Count(r => r.Status == ReferralStatus.Accepted);

            var response = new Response(
                Code: code.Code,
                TimesUsed: code.TimesUsed,
                QualifiedCount: qualified,
                AcceptedCount: accepted,
                PointsPerReferral: ReferralPolicy.PointsPerSide);

            return BusinessResult.Success(response);
        }
    }
}
