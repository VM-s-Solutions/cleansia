using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.Referrals;

/// <summary>
/// Paged list of "people I invited" for the inviter's referrals tab.
/// Privacy: returns only the invitee's first name (e.g. "Jana") rather than
/// the full name + email — enough for the UI greeting without leaking PII.
/// </summary>
public class GetMyReferrals
{
    public record Query(string UserId = "", int Offset = 0, int Limit = 20)
        : IRequest<PagedData<ReferralListItem>>;

    public record ReferralListItem(
        string Id,
        string ReferredFirstName,
        ReferralStatus Status,
        DateTimeOffset AcceptedOn,
        DateTimeOffset? FirstQualifyingOrderOn,
        int? PointsAwardedToReferrer);

    internal class Handler(
        IReferralRepository referralRepository) : IRequestHandler<Query, PagedData<ReferralListItem>>
    {
        public async Task<PagedData<ReferralListItem>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pageNumber = request.Limit > 0 ? (request.Offset / request.Limit) + 1 : 1;

            if (string.IsNullOrEmpty(request.UserId))
            {
                return new PagedData<ReferralListItem>(pageNumber, request.Limit, 0, Array.Empty<ReferralListItem>());
            }

            var totalItems = await referralRepository.CountByReferrerAsync(request.UserId, cancellationToken);
            var referrals = await referralRepository.GetByReferrerAsync(
                request.UserId, request.Offset, request.Limit, cancellationToken);

            // Repository .Include(r => r.Referred) populates the invitee for
            // first-name resolution.
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
