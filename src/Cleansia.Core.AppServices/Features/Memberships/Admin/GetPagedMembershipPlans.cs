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
/// Admin-side paged list of membership plans (active AND inactive) with an optional active filter and
/// a case-insensitive code/name search. Each row shows the platform-default-currency price, null when
/// the plan has none in it.
/// </summary>
public class GetPagedMembershipPlans
{
    public class Request : DataRangeRequest, IRequest<PagedData<MembershipPlanListItem>>
    {
        public bool? Active { get; init; }
        public string? Search { get; init; }
    }

    internal class Handler(
        IMembershipPlanRepository membershipPlanRepository,
        IMembershipPlanPriceRepository membershipPlanPriceRepository,
        ICurrencyRepository currencyRepository)
        : IRequestHandler<Request, PagedData<MembershipPlanListItem>>
    {
        public async Task<PagedData<MembershipPlanListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = MembershipPlanSpecification.Create(
                isActive: request.Active,
                search: request.Search);

            var filter = specification.SatisfiedBy();

            var total = await membershipPlanRepository.GetCountAsync(filter, cancellationToken);
            var plans = await membershipPlanRepository
                .GetPagedSort<MembershipPlanSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var currency = await currencyRepository.GetDefaultAsync(cancellationToken);
            var prices = await membershipPlanPriceRepository.GetForPlansAsync(
                plans.Select(p => p.Id).ToList(), currency.Id, cancellationToken);

            var data = plans
                .Select(plan => plan.MapToListItem(
                    prices.TryGetValue(plan.Id, out var price) ? price.Price : null,
                    currency.Code))
                .ToList();

            return data.MapToDto(total, request);
        }

        private static IEnumerable<SortDefinition> ResolveSort(Request request)
        {
            var sort = request.Sort.MapToDomain().ToList();
            return sort.Count > 0
                ? sort
                :
                [
                    new SortDefinition { Field = nameof(MembershipPlan.BillingInterval), Direction = SortDirection.Ascending },
                    new SortDefinition { Field = nameof(MembershipPlan.Code), Direction = SortDirection.Ascending },
                ];
        }
    }
}
