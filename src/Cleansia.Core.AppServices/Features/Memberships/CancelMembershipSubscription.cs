using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Memberships;

public class CancelMembershipSubscription
{
    public record Command : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
    }

    public record Response(DateTime EffectiveEndDate);

    public class Handler(
        IUserMembershipRepository userMembershipRepository,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var membership = await userMembershipRepository.GetActiveForUserAsync(userId, cancellationToken);
            if (membership == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(Command), BusinessErrorMessage.MembershipNotFound));
            }

            try
            {
                await stripeClient.CancelSubscriptionAtPeriodEndAsync(
                    membership.StripeSubscriptionId, cancellationToken);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Stripe subscription cancellation failed for membership {MembershipId}", membership.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(membership.StripeSubscriptionId), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            membership.MarkCancellationRequested();

            logger.LogInformation(
                "Cancellation requested for membership {MembershipId}; effective {EndDate}",
                membership.Id, membership.CurrentPeriodEnd);

            return BusinessResult.Success(new Response(membership.CurrentPeriodEnd));
        }
    }
}
