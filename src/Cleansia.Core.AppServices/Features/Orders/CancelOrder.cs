using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

[AuditAction("customer.order.cancel", Audience = AuditAudience.Customer, ResourceType = "Order")]
public class CancelOrder
{
    public record Command(
        string OrderId,
        string? Reason
    ) : ICommand<Response>;

    public record Response(
        string OrderId,
        decimal FeeRate,
        decimal RefundAmount,
        decimal TotalPrice,
        bool RefundInitiated,
        decimal? ActualRefundAmount = null);

    /// <summary>
    /// What the cancel cost and why, as the server computed it at the click (ADR-0062 D3). The reason
    /// text stays on <c>Order.CancellationReason</c>; the row records only that one was given.
    /// </summary>
    public record OrderCancellationEvidence(
        CancellationFeeTier Tier,
        decimal FeeRate,
        decimal FeeAmount,
        decimal RefundAmount,
        decimal TotalPrice,
        string CurrencyId,
        bool HasBeenAccepted,
        decimal HoursBeforeCleaning,
        decimal MinutesSinceBooking,
        int FreeCancellationHoursApplied,
        CancellationPolicyFigures PolicyFigures,
        bool ExpressWaiverReleased,
        bool RefundInitiated,
        PaymentType PaymentType,
        PaymentStatus PaymentStatus,
        bool ReasonProvided,
        decimal? ActualRefundAmount = null) : ICustomerAuditPayload;

    public record CancellationPolicyFigures(
        int FreeHours,
        int PartialHours,
        decimal PartialRate,
        decimal LastMinuteRate,
        int OopsMinutesStandard,
        int OopsMinutesFirstTime)
    {
        public static CancellationPolicyFigures Current() => new(
            BookingPolicy.FreeCancellationHours,
            BookingPolicy.PartialCancellationHours,
            BookingPolicy.PartialCancellationFeeRate,
            BookingPolicy.LastMinuteCancellationFeeRate,
            BookingPolicy.OopsWindowMinutesStandard,
            BookingPolicy.OopsWindowMinutesFirstTime);
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.Reason)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IOrderAccessService orderAccessService,
        IUserSessionProvider userSessionProvider,
        CustomerOrderCancellation cancellation
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            // Missing and foreign orders share the handler's refusal and failure-audit path.
            var order = await orderAccessService
                .OrdersForCaller()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(ae => ae.Employee)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            if (order.UserId != userId)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            if (CancellationAssessor.BlockedReason(order) is { } blockedReason)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    blockedReason));
            }

            var result = await cancellation.ExecuteAsync(order, command.Reason, userId, cancellationToken);
            return BusinessResult.Success(result.Response);
        }
    }
}
