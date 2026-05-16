using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Memberships;

public class GetMyMembership
{
    public record Query : ICommand<Response>;

    public record Response(
        bool HasMembership,
        string? PlanCode,
        string? PlanName,
        decimal? MonthlyPriceCzk,
        decimal? DiscountPercentage,
        int? FreeCancellationWindowHours,
        bool? AllowsExpressUpgrade,
        MembershipStatus? Status,
        DateTime? CurrentPeriodEnd,
        bool CancelRequested,
        int? BillingInterval,
        decimal? MonthlyEquivalentPriceCzk);

    public class Handler(
        IUserMembershipRepository userMembershipRepository,
        IUserSessionProvider userSessionProvider)
        : ICommandHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var membership = await userMembershipRepository.GetActiveForUserAsync(userId, cancellationToken);
            if (membership == null)
            {
                return BusinessResult.Success(new Response(
                    HasMembership: false,
                    PlanCode: null,
                    PlanName: null,
                    MonthlyPriceCzk: null,
                    DiscountPercentage: null,
                    FreeCancellationWindowHours: null,
                    AllowsExpressUpgrade: null,
                    Status: null,
                    CurrentPeriodEnd: null,
                    CancelRequested: false,
                    BillingInterval: null,
                    MonthlyEquivalentPriceCzk: null));
            }

            return BusinessResult.Success(new Response(
                HasMembership: true,
                PlanCode: membership.MembershipPlan.Code,
                PlanName: membership.MembershipPlan.Name,
                MonthlyPriceCzk: membership.MembershipPlan.MonthlyPriceCzk,
                DiscountPercentage: membership.MembershipPlan.DiscountPercentage,
                FreeCancellationWindowHours: membership.MembershipPlan.FreeCancellationWindowHours,
                AllowsExpressUpgrade: membership.MembershipPlan.AllowsExpressUpgrade,
                Status: membership.Status,
                CurrentPeriodEnd: membership.CurrentPeriodEnd,
                CancelRequested: membership.CancelledAt.HasValue,
                BillingInterval: (int)membership.MembershipPlan.BillingInterval,
                MonthlyEquivalentPriceCzk: membership.MembershipPlan.MonthlyEquivalentPriceCzk));
        }
    }
}
