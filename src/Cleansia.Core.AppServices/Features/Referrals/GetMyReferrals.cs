using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.Referrals;

public class GetMyReferrals
{
    public record Query(int Offset = 0, int Limit = 20)
        : IRequest<PagedData<ReferralListItem>>;

    public record ReferralListItem(
        string Id,
        string ReferredFirstName,
        ReferralStatus Status,
        DateTimeOffset AcceptedOn,
        DateTimeOffset? FirstQualifyingOrderOn,
        int? PointsAwardedToReferrer);

    internal class Handler(
        IReferralRepository referralRepository,
        IUserSessionProvider userSessionProvider) : IRequestHandler<Query, PagedData<ReferralListItem>>
    {
        public async Task<PagedData<ReferralListItem>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pageNumber = request.Limit > 0 ? (request.Offset / request.Limit) + 1 : 1;
            var userId = userSessionProvider.GetUserId();

            if (string.IsNullOrEmpty(userId))
            {
                return new PagedData<ReferralListItem>(pageNumber, request.Limit, 0, Array.Empty<ReferralListItem>());
            }

            var totalItems = await referralRepository.CountByReferrerAsync(userId, cancellationToken);
            var referrals = await referralRepository.GetByReferrerAsync(
                userId, request.Offset, request.Limit, cancellationToken);

            var items = referrals
                .Select(r => new ReferralListItem(
                    Id: r.Id,
                    ReferredFirstName: r.Referred?.FirstName ?? string.Empty,
                    Status: r.Status,
                    AcceptedOn: r.AcceptedOn,
                    FirstQualifyingOrderOn: r.FirstQualifyingOrderOn,
                    PointsAwardedToReferrer: r.PointsAwardedToReferrer))
                .ToList();

            return new PagedData<ReferralListItem>(pageNumber, request.Limit, totalItems, items);
        }
    }
}
