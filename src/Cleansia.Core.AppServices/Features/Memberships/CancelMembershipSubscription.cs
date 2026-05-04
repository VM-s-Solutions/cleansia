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
/// Mark the user's active membership for cancellation at the end of the
/// current billing period. Stripe keeps benefits flowing until period end,
/// then fires <c>customer.subscription.deleted</c> which the webhook handler
/// transitions to <see cref="Cleansia.Core.Domain.Memberships.MembershipStatus.Cancelled"/>.
/// </summary>
public class CancelMembershipSubscription
{
    public record Command(string UserId = "") : ICommand<Response>;

    public record Response(string MembershipId, DateTime EffectiveEndDate);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IUserMembershipRepository userMembershipRepository,
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

            await stripeClient.CancelSubscriptionAtPeriodEndAsync(
                membership.StripeSubscriptionId, cancellationToken);

            membership.MarkCancellationRequested();

            logger.LogInformation(
                "User {UserId} requested cancellation of membership {MembershipId}; effective {EndDate}",
                command.UserId, membership.Id, membership.CurrentPeriodEnd);

            return BusinessResult.Success(new Response(
                MembershipId: membership.Id,
                EffectiveEndDate: membership.CurrentPeriodEnd));
        }
    }
}
