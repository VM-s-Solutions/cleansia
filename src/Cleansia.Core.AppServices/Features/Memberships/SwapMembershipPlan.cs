using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Memberships;

/// <summary>
/// Swap an active membership to a different plan — typically the
/// monthly → yearly upgrade after the user has subscribed and decided
/// they like Plus. Stripe prorates the cost difference and charges/credits
/// the customer's default payment method on the spot.
///
/// This is intentionally separate from <see cref="CreateMembershipSubscription"/>
/// because the flows are different: there's no SetupIntent here (we reuse
/// the existing payment method), no two-phase dance, just a single Stripe
/// update + a local mirror sync. New subscribers without an existing
/// membership should still go through CreateMembershipSubscription.
/// </summary>
public class SwapMembershipPlan
{
    public record Command(string NewPlanCode, string UserId = "") : ICommand<Response>;

    public record Response(
        string MembershipId,
        string NewPlanCode,
        DateTime CurrentPeriodEnd);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.NewPlanCode).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.UserId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IUserMembershipRepository userMembershipRepository,
        IMembershipPlanRepository membershipPlanRepository,
        IStripeClient stripeClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var membership = await userMembershipRepository.GetActiveForUserAsync(command.UserId, cancellationToken);
            if (membership == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.UserId), BusinessErrorMessage.MembershipNotFound));
            }

            var newPlan = await membershipPlanRepository.GetByCodeAsync(command.NewPlanCode, cancellationToken);
            if (newPlan == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.NewPlanCode), BusinessErrorMessage.MembershipPlanNotFound));
            }

            if (membership.MembershipPlanId == newPlan.Id)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.NewPlanCode), BusinessErrorMessage.MembershipSwapSamePlan));
            }

            var swapped = await stripeClient.SwapSubscriptionPriceAsync(
                membership.StripeSubscriptionId,
                newPlan.StripePriceId,
                cancellationToken);

            membership.ApplyPlanSwap(
                newMembershipPlanId: newPlan.Id,
                currentPeriodStart: swapped.CurrentPeriodStart,
                currentPeriodEnd: swapped.CurrentPeriodEnd);

            logger.LogInformation(
                "Swapped membership {MembershipId} (sub {SubscriptionId}) to plan {NewPlanCode}, new period end {PeriodEnd}",
                membership.Id, membership.StripeSubscriptionId, newPlan.Code, swapped.CurrentPeriodEnd);

            return BusinessResult.Success(new Response(
                MembershipId: membership.Id,
                NewPlanCode: newPlan.Code,
                CurrentPeriodEnd: swapped.CurrentPeriodEnd));
        }
    }
}
