using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Memberships;

[AuditAction("customer.membership.cancel", Audience = AuditAudience.Customer, ResourceType = "UserMembership")]
public class CancelMembershipSubscription
{
    public record Command : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
    }

    public record Response(DateTime EffectiveEndDate);

    /// <summary>The plan being given up and the date the benefits run out (ADR-0062 D3).</summary>
    public record MembershipCancelEvidence(
        string? PlanCode,
        DateTimeOffset CurrentPeriodEndsAt) : ICustomerAuditPayload;

    public class Handler(
        IUserMembershipRepository userMembershipRepository,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        IAuditContext auditContext,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var membership = await userMembershipRepository.GetActiveForUserAsync(userId, cancellationToken);
            if (membership == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(userId), BusinessErrorMessage.MembershipNotFound));
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

            auditContext.RecordEvidence("UserMembership", membership.Id, new MembershipCancelEvidence(
                PlanCode: membership.MembershipPlan?.Code,
                CurrentPeriodEndsAt: new DateTimeOffset(DateTime.SpecifyKind(membership.CurrentPeriodEnd, DateTimeKind.Utc))));

            return BusinessResult.Success(new Response(membership.CurrentPeriodEnd));
        }
    }
}
