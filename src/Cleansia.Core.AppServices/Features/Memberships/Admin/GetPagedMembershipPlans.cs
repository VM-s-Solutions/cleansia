using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.AppServices.Features.Memberships.Admin.Mappers;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SortDefinition = Cleansia.Core.Domain.Sorting.Common.SortDefinition;
using SortDirection = Cleansia.Core.Domain.Sorting.Common.SortDirection;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// Admin-side paged list of membership plans (active AND inactive) with an
/// optional active filter and a case-insensitive code/name search. Drives the
/// Memberships admin module's plans table.
/// </summary>
public class GetPagedMembershipPlans
{
    public class Request : DataRangeRequest, IRequest<PagedData<MembershipPlanListItem>>
    {
        public bool? Active { get; init; }
        public string? Search { get; init; }
    }

    internal class Handler(IMembershipPlanRepository membershipPlanRepository)
        : IRequestHandler<Request, PagedData<MembershipPlanListItem>>
    {
        public async Task<PagedData<MembershipPlanListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = MembershipPlanSpecification.Create(
                isActive: request.Active,
                search: request.Search);

            var filter = specification.SatisfiedBy();

            var total = await membershipPlanRepository.GetCountAsync(filter, cancellationToken);
            // MapToListItem reads MonthlyEquivalentPriceCzk (a computed property), so the rows are
            // materialized first and mapped in memory rather than projected in the query.
            var plans = await membershipPlanRepository
                .GetPagedSort<MembershipPlanSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var data = plans.Select(plan => plan.MapToListItem()).ToList();

            return data.MapToDto(total, request);
        }

        // Preserves the historical default order: the bespoke repo ordered by
        // BillingInterval then MonthlyPriceCzk, and the empty-sort GetPagedSort path applies none.
        private static IEnumerable<SortDefinition> ResolveSort(Request request)
        {
            var sort = request.Sort.MapToDomain().ToList();
            return sort.Count > 0
                ? sort
                :
                [
                    new SortDefinition { Field = nameof(MembershipPlan.BillingInterval), Direction = SortDirection.Ascending },
                    new SortDefinition { Field = nameof(MembershipPlan.MonthlyPriceCzk), Direction = SortDirection.Ascending },
                ];
        }
    }
}
