using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Memberships;

public class SwapMembershipPlan
{
    public record Command(string NewPlanCode) : ICommand<Response>;

    public record Response(
        string NewPlanCode,
        DateTime CurrentPeriodEnd);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.NewPlanCode).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IUserMembershipRepository userMembershipRepository,
        IMembershipPlanRepository membershipPlanRepository,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        IStripeConfig stripeConfig,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {

            // Card payments can be switched off platform-wide, and a membership is a card charge like
            // any other — the first invoice bills immediately. Checked before any Stripe object is
            // created. -> IStripeConfig
            if (!stripeConfig.Enabled)
            {
                logger.LogWarning("Membership plan swap refused: card payments are disabled (Stripe:Enabled=false)");
                return BusinessResult.Failure<Response>(new Error(
                    "MembershipPlanId", BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            var userId = userSessionProvider.GetUserId()!;
            var membership = await userMembershipRepository.GetActiveForUserAsync(userId, cancellationToken);
            if (membership == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(userId), BusinessErrorMessage.MembershipNotFound));
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

            // Fresh attempt id so A→B→A→B-style swaps each reach Stripe
            // instead of replaying the first swap's response.
            var attemptId = Guid.NewGuid().ToString("N");
            SubscriptionResult swapped;
            try
            {
                swapped = await stripeClient.SwapSubscriptionPriceAsync(
                    membership.StripeSubscriptionId,
                    newPlan.StripePriceId,
                    attemptId,
                    cancellationToken);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Stripe subscription swap failed for membership {MembershipId}", membership.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.NewPlanCode), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            membership.ApplyPlanSwap(
                newMembershipPlanId: newPlan.Id,
                currentPeriodStart: swapped.CurrentPeriodStart,
                currentPeriodEnd: swapped.CurrentPeriodEnd);

            logger.LogInformation(
                "Swapped membership {MembershipId} (sub {SubscriptionId}) to plan {NewPlanCode}, new period end {PeriodEnd}",
                membership.Id, membership.StripeSubscriptionId, newPlan.Code, swapped.CurrentPeriodEnd);

            return BusinessResult.Success(new Response(
                NewPlanCode: newPlan.Code,
                CurrentPeriodEnd: swapped.CurrentPeriodEnd));
        }
    }
}
