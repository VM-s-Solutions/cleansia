using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// What cancelling this order right now would cost, asked BEFORE the customer commits.
///
/// <para>A read, deliberately: no status track, no refund, no loyalty change, and nothing consumed or
/// reserved — a cancel sheet may open, close and re-open without spending anything. It is a
/// <c>Query</c> rather than a <c>Command</c>, so it does not ride the unit-of-work commit at all
/// (<c>UnitOfWorkPipelineBehavior</c> keys on the request type name).</para>
/// </summary>
public class GetCancellationFeePreview
{
    public record Query(string OrderId) : IQuery<Response>;

    /// <param name="Tier">
    /// Why the fee is what it is, so a client renders the reason instead of rebuilding the schedule.
    /// The two facts that decide it — the caller's own free-cancellation window (a Plus plan's is 4
    /// hours where the standard is 24) and whether a cleaner has actually been pulled onto the job —
    /// are server-side, so no client can derive this correctly even in principle.
    /// </param>
    /// <param name="FeeAmount">
    /// The charge, in the order's currency. Always <c>TotalPrice - RefundAmount</c>, so the two halves
    /// the customer is shown sum to the total they paid.
    /// </param>
    /// <param name="ExpressWaiverForfeitedOnCancel">
    /// ADR-0035 AM-13 — cancelling would use up one of this month's free express bookings. Disclosed
    /// here because the cases where the forfeiture is otherwise invisible (inside the oops window, or
    /// on any cash order) are exactly the ones where the fee is zero.
    /// </param>
    public record Response(
        string OrderId,
        CancellationFeeTier Tier,
        decimal FeeRate,
        decimal FeeAmount,
        decimal RefundAmount,
        decimal TotalPrice,
        string CurrencyCode,
        bool ExpressWaiverForfeitedOnCancel);

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider,
        ICancellationPolicyResolver cancellationPolicyResolver,
        IExpressWaiverConsumer expressWaiverConsumer) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            // AssignedEmployees without the Employee graph: the fee needs the row's existence, and the
            // customer is owed the price, not the roster.
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .Include(o => o.Currency)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == query.OrderId, cancellationToken);

            if (order == null || order.UserId != userId)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(query.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            if (CancellationAssessor.BlockedReason(order) is { } blockedReason)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(query.OrderId),
                    blockedReason));
            }

            var policy = await cancellationPolicyResolver.ResolveForUserAsync(userId, cancellationToken);
            var assessment = CancellationAssessor.Assess(order, policy, DateTime.UtcNow);

            var expressWaiverForfeited = await expressWaiverConsumer.WouldForfeitOnCustomerCancelAsync(
                order.Id, assessment.HasBeenAccepted, cancellationToken);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                Tier: assessment.Tier,
                FeeRate: assessment.FeeRate,
                FeeAmount: assessment.FeeAmount,
                RefundAmount: assessment.RefundAmount,
                TotalPrice: order.TotalPrice,
                CurrencyCode: order.Currency.Code,
                ExpressWaiverForfeitedOnCancel: expressWaiverForfeited));
        }
    }
}
