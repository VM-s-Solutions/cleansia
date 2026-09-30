using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// An administrator confirms that the cleaner did not arrive (owner ruling 2026-09-28): the order is
/// cancelled with the no-show sweep's own remedy — no fee, the whole refund, the apology credit and the
/// no-cleaner message — and a linked "service not provided" dispute is closed. A crew with one of two
/// cleaners missing stays a dispute for the administrator's judgement.
/// → /product/business-rules#when-the-cleaner-cancels-or-no-shows
/// </summary>
[AuditAction("order.cancel.no_show", ResourceType = "Order")]
public class AdminCancelOrderAsNoShow
{
    public record Command(string OrderId) : ICommand<Response>;

    /// <param name="RefundedAmount">The card refund that went through, or null.</param>
    /// <param name="RefundPending">A card refund was owed and did not go through; it is re-driven hourly.</param>
    /// <param name="ApologyCredit">The apology credit issued, or null (a guest, or a currency with none authored).</param>
    public record Response(
        string OrderId,
        decimal? RefundedAmount,
        bool RefundPending,
        decimal? ApologyCredit);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider,
        CleanerNoShowCancellation noShowCancellation,
        INotificationProducer notificationProducer,
        ILiveActivityProducer liveActivityProducer,
        IExpressWaiverConsumer expressWaiverConsumer,
        ILoyaltyService loyaltyService,
        TimeProvider timeProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var adminId = userSessionProvider.GetUserId()!;
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(ae => ae.Employee)
                .Include(o => o.Currency)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            // A cleaner who tapped Start after the customer's report did arrive: the report stands as a
            // dispute for the administrator, and this refuses.
            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            if (CleanerNoShow.RefusalFor(order, nowUtc) is { } refusal)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), refusal));
            }

            var outcome = await noShowCancellation.ExecuteAsync(
                order, CancelledBy.Admin, adminId, nowUtc, cancellationToken);

            await liveActivityProducer.NotifyOrderTransitionAsync(
                order, LiveActivityEventKeys.End, outcome.Transition, cancellationToken);
            await expressWaiverConsumer.ReleaseForOrderAsync(order.Id, cancellationToken);
            await OrderAssignmentCancellationNotifier.NotifyAssignedEmployeesOfCancellationAsync(
                order, notificationProducer, cancellationToken);
            await loyaltyService.RevokeForCancelledOrderAsync(order.Id, cancellationToken);

            var dispute = await disputeRepository.GetOpenDisputeForOrderAsync(order.Id, cancellationToken);
            if (dispute is { Reason: DisputeReason.ServiceNotProvided })
            {
                dispute.UpdateStatus(DisputeStatus.Closed, adminId);
            }

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                RefundedAmount: outcome.RefundedAmount,
                RefundPending: outcome.RefundPending,
                ApologyCredit: outcome.ApologyAmount));
        }
    }
}
