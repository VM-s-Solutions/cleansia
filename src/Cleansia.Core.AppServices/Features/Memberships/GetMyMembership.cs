using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// "What's my Plus status?" — drives the My Membership profile screen.
/// Returns either the user's active/pending-cancel membership snapshot, or
/// a "no membership" response that the UI uses to show the upgrade CTA.
/// </summary>
public class GetMyMembership
{
    public record Query(string UserId = "") : ICommand<Response>;

    public record Response(
        bool HasMembership,
        string? MembershipId,
        string? PlanCode,
        string? PlanName,
        decimal? MonthlyPriceCzk,
        decimal? DiscountPercentage,
        int? FreeCancellationWindowHours,
        bool? AllowsExpressUpgrade,
        MembershipStatus? Status,
        DateTime? CurrentPeriodEnd,
        bool CancelRequested,
        /// <summary>
        /// 1 = Monthly, 2 = Yearly. Drives the "Switch to annual" CTA gating
        /// in the management UI — only show when the user is on a Monthly plan.
        /// </summary>
        int? BillingInterval,
        /// <summary>
        /// Per-month equivalent price (annual/12 for yearly, same as price
        /// for monthly). Lets the UI show "199 Kč/month" without the client
        /// needing to know about billing intervals.
        /// </summary>
        decimal? MonthlyEquivalentPriceCzk);

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(IUserMembershipRepository userMembershipRepository) : ICommandHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var membership = await userMembershipRepository.GetActiveForUserAsync(query.UserId, cancellationToken);
            if (membership == null)
            {
                return BusinessResult.Success(new Response(
                    HasMembership: false,
                    MembershipId: null,
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
                MembershipId: membership.Id,
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
