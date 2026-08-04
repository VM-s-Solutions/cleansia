using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Services.Interfaces;
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
        decimal? MonthlyEquivalentPriceCzk,
        /// <summary>The plan's free express upgrades per calendar month. Null when there is no membership.</summary>
        int? ExpressUpgradesPerMonth = null,
        /// <summary>
        /// Live waivers left this calendar month, the single "before any booking under consideration"
        /// definition. Null = no membership (the client renders the non-member state); 0 = exhausted, OR
        /// still inside the trial. The two zeros are told apart by <see cref="TrialEndsAtUtc"/>, because a
        /// trialing member is neither a non-member nor someone who used theirs up.
        /// </summary>
        int? ExpressUpgradesRemaining = null,
        /// <summary>
        /// End of the Stripe free trial. Non-null and in the future means metered benefits have not
        /// started yet, so the client can say when they do instead of rendering a bare zero.
        /// </summary>
        DateTime? TrialEndsAtUtc = null,
        /// <summary>
        /// False once this customer has had their one free trial (owner ruling 2026-08-03), on any past
        /// enrolment. The server refuses the second trial either way; this is what stops a subscribe
        /// screen from advertising one it will not receive. Plan-independent — combine with the plan's
        /// own <c>TrialPeriodDays</c>. Defaults true so a client built before this field renders exactly
        /// as it does today.
        /// </summary>
        bool TrialEligible = true);

    public class Handler(
        IUserMembershipRepository userMembershipRepository,
        IUserSessionProvider userSessionProvider,
        IExpressWaiverResolver expressWaiverResolver)
        : ICommandHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var trialEligible = !await userMembershipRepository.HasEverStartedTrialAsync(userId, cancellationToken);
            var membership = await userMembershipRepository.GetActiveForUserNoTrackingAsync(userId, cancellationToken);
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
                    MonthlyEquivalentPriceCzk: null,
                    TrialEligible: trialEligible));
            }

            // One collaborator, not two: the resolver is the ONE place the remaining count and the period
            // key are computed, so this handler never acquires a usage repository and a period-key source
            // separately and never grows a second definition of "remaining".
            var waiver = await expressWaiverResolver.ResolveForUserAsync(
                userId, cleaningUtc: null, DateTime.UtcNow, cancellationToken);

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
                MonthlyEquivalentPriceCzk: membership.MembershipPlan.MonthlyEquivalentPriceCzk,
                // The resolver's quota, not the plan column: a plan with the flag off carries a number the
                // pricing path ignores, and rendering it beside a zero remaining reads as "exhausted".
                ExpressUpgradesPerMonth: waiver.Quota,
                ExpressUpgradesRemaining: waiver.RemainingBeforeThisBooking,
                TrialEndsAtUtc: membership.TrialEndsAtUtc,
                TrialEligible: trialEligible));
        }
    }
}
