using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.AppServices.Features.Memberships.Admin.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>Single membership-plan admin detail with every currency's price row, keyed by currency code.</summary>
public class GetMembershipPlanById
{
    public record Query(string MembershipPlanId) : IQuery<MembershipPlanDetailDto>;

    public class Handler(
        IMembershipPlanRepository membershipPlanRepository,
        IMembershipPlanPriceRepository membershipPlanPriceRepository)
        : IQueryHandler<Query, MembershipPlanDetailDto>
    {
        public async Task<BusinessResult<MembershipPlanDetailDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var plan = await membershipPlanRepository.GetByIdAsync(request.MembershipPlanId, cancellationToken);

            if (plan == null)
            {
                return BusinessResult.Failure<MembershipPlanDetailDto>(
                    new Error(nameof(request.MembershipPlanId), BusinessErrorMessage.MembershipPlanNotFound));
            }

            var prices = await membershipPlanPriceRepository.GetAllForPlanAsync(plan.Id, cancellationToken);

            return BusinessResult.Success(plan.MapToDetailDto(prices));
        }
    }
}
