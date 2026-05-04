using Cleansia.Core.AppServices.Features.Referrals.Admin.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.Referrals.Admin;

/// <summary>
/// Admin paged list of referrals across all users. Filters: status,
/// AcceptedOn date range. Pagination is repository-materialised (a single
/// query yielding the page + total count).
/// </summary>
public class GetPagedReferrals
{
    public record Query(
        ReferralStatus? Status = null,
        DateTimeOffset? DateFrom = null,
        DateTimeOffset? DateTo = null,
        int Offset = 0,
        int Limit = 20) : IRequest<PagedData<AdminReferralListItem>>;

    internal class Handler(IReferralRepository referralRepository)
        : IRequestHandler<Query, PagedData<AdminReferralListItem>>
    {
        public async Task<PagedData<AdminReferralListItem>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pageNumber = request.Limit > 0 ? (request.Offset / request.Limit) + 1 : 1;

            var (rows, total) = await referralRepository.GetPagedAdminAsync(
                request.Status,
                request.DateFrom,
                request.DateTo,
                request.Offset,
                request.Limit,
                cancellationToken);

            var items = rows
                .Select(r => new AdminReferralListItem(
                    Id: r.Id,
                    ReferrerUserId: r.ReferrerUserId,
                    ReferrerEmail: r.Referrer?.Email,
                    ReferredUserId: r.ReferredUserId,
                    ReferredEmail: r.Referred?.Email,
                    Status: r.Status,
                    AcceptedOn: r.AcceptedOn,
                    FirstQualifyingOrderOn: r.FirstQualifyingOrderOn,
                    PointsAwardedToReferrer: r.PointsAwardedToReferrer,
                    PointsAwardedToReferred: r.PointsAwardedToReferred))
                .ToList();

            return new PagedData<AdminReferralListItem>(pageNumber, request.Limit, total, items);
        }
    }
}
