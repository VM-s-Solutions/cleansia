using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.AppServices.Features.Memberships.Admin.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using MediatR;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// Admin-side paged list of membership plans (active AND inactive) with an
/// optional active filter and a case-insensitive code/name search. Drives the
/// Memberships admin module's plans table.
/// </summary>
public class GetPagedMembershipPlans
{
    public record Query(
        bool? Active = null,
        string? Search = null,
        int Offset = 0,
        int Limit = 20) : IRequest<PagedData<MembershipPlanListItem>>;

    public class Handler(IMembershipPlanRepository membershipPlanRepository)
        : IRequestHandler<Query, PagedData<MembershipPlanListItem>>
    {
        public async Task<PagedData<MembershipPlanListItem>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pageNumber = request.Limit > 0 ? (request.Offset / request.Limit) + 1 : 1;

            var (items, total) = await membershipPlanRepository.GetPagedAdminAsync(
                request.Active,
                request.Search,
                request.Offset,
                request.Limit,
                cancellationToken);

            var data = items.Select(p => p.MapToListItem()).ToList();

            return new PagedData<MembershipPlanListItem>(pageNumber, request.Limit, total, data);
        }
    }
}
