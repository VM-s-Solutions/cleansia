using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.AppServices.Features.Memberships.Admin.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// Single membership-plan admin detail. Returns the persisted shape including
/// the admin-entered <see cref="Cleansia.Core.Domain.Memberships.MembershipPlan.StripePriceId"/>.
/// </summary>
public class GetMembershipPlanById
{
    public record Query(string MembershipPlanId) : IQuery<MembershipPlanDetailDto>;

    public class Handler(IMembershipPlanRepository membershipPlanRepository)
        : IQueryHandler<Query, MembershipPlanDetailDto>
    {
        public async Task<BusinessResult<MembershipPlanDetailDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var plan = await membershipPlanRepository.GetByIdAsync(request.MembershipPlanId, cancellationToken);

            if (plan == null)
            {
                return BusinessResult.Failure<MembershipPlanDetailDto>(
                    new Error(BusinessErrorMessage.MembershipPlanNotFound, BusinessErrorMessage.MembershipPlanNotFound));
            }

            return BusinessResult.Success(plan.MapToDetailDto());
        }
    }
}
