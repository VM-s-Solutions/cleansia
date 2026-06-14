#nullable enable
using Cleansia.Core.AppServices.Features.Referrals.Admin.DTOs;
using Cleansia.Core.AppServices.Features.Referrals.Admin.Filters;
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

namespace Cleansia.Core.AppServices.Features.Referrals.Admin;

public class GetPagedReferrals
{
    public class Request : DataRangeRequest, IRequest<PagedData<AdminReferralListItem>>
    {
        public ReferralFilter? Filter { get; init; }
    }

    internal class Handler(IReferralRepository referralRepository)
        : IRequestHandler<Request, PagedData<AdminReferralListItem>>
    {
        public async Task<PagedData<AdminReferralListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = ReferralSpecification.Create(
                status: request.Filter?.Status,
                acceptedFrom: request.Filter?.DateFrom,
                acceptedTo: request.Filter?.DateTo);

            var filter = specification.SatisfiedBy();

            var totalItems = await referralRepository.GetCountAsync(filter, cancellationToken);
            var items = await referralRepository
                .GetPagedSort<ReferralSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .Include(r => r.Referrer)
                .Include(r => r.Referred)
                .AsNoTracking()
                .Select(referral => referral.MapToAdminListItem())
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
