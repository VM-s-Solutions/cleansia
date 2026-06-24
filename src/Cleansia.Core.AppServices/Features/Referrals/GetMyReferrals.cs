using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SortDefinition = Cleansia.Core.Domain.Sorting.Common.SortDefinition;
using SortDirection = Cleansia.Core.Domain.Sorting.Common.SortDirection;

namespace Cleansia.Core.AppServices.Features.Referrals;

public class GetMyReferrals
{
    public class Request : DataRangeRequest, IRequest<PagedData<ReferralListItem>>;

    public record ReferralListItem(
        string Id,
        string ReferredFirstName,
        ReferralStatus Status,
        DateTimeOffset AcceptedOn,
        DateTimeOffset? FirstQualifyingOrderOn,
        int? PointsAwardedToReferrer);

    internal class Handler(
        IReferralRepository referralRepository,
        IUserSessionProvider userSessionProvider) : IRequestHandler<Request, PagedData<ReferralListItem>>
    {
        public async Task<PagedData<ReferralListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId();

            if (string.IsNullOrEmpty(userId))
            {
                return Enumerable.Empty<ReferralListItem>().MapToDto(0, request);
            }

            var specification = ReferralSpecification.Create(referrerUserId: userId);
            var filter = specification.SatisfiedBy();

            var totalItems = await referralRepository.GetCountAsync(filter, cancellationToken);
            var items = await referralRepository
                .GetPagedSort<ReferralSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .Include(r => r.Referred)
                .AsNoTracking()
                .Select(referral => referral.MapToMyListItem())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }

        // Preserves the historical newest-first default: the bespoke repo ordered by
        // AcceptedOn desc, and the empty-sort GetPagedSort path applies no ordering.
        private static IEnumerable<SortDefinition> ResolveSort(Request request)
        {
            var sort = request.Sort.MapToDomain().ToList();
            return sort.Count > 0
                ? sort
                : [new SortDefinition { Field = nameof(Referral.AcceptedOn), Direction = SortDirection.Descending }];
        }
    }
}
