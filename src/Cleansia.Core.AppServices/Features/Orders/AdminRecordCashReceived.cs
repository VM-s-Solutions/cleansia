using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// An administrator records a cash handover the cleaner could not record themselves (owner ruling
/// 2026-09-28): which assigned cleaner took the cash, when, and how much. The order is then paid exactly
/// as the cleaner's own <see cref="MarkCashCollected"/> would have paid it, so the cleaner can complete
/// it; on an order an administrator already completed, the cash receipt is issued here, because that is
/// the moment both of its dates exist. → /flows/payment-and-fiscal
/// </summary>
[AuditAction("order.cash.record", ResourceType = "Order")]
public class AdminRecordCashReceived
{
    public record Command(string OrderId, string EmployeeId, DateTime ReceivedAt, decimal Amount) : ICommand<Response>;

    public record Response(string OrderId, PaymentStatus PaymentStatus);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;

        public Validator(IOrderRepository orderRepository, TimeProvider timeProvider)
        {
            _orderRepository = orderRepository;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(async (orderId, ct) => (await LoadAsync(orderId, ct))?.CurrentStatus != OrderStatus.Cancelled)
                .WithMessage(BusinessErrorMessage.OrderAlreadyCancelled)
                // Cash changes hands at the door, so before the clean has begun there is none to record.
                .MustAsync(async (orderId, ct) =>
                    (await LoadAsync(orderId, ct))?.CurrentStatus is OrderStatus.InProgress or OrderStatus.Completed)
                .WithMessage(BusinessErrorMessage.OrderNotInProgress)
                // A card booking whose charge never arrived is the cleaner's reconciliation, which asks
                // Stripe first; an administrator repairs it with the status override.
                .MustAsync(async (orderId, ct) => (await LoadAsync(orderId, ct))?.PaymentType == PaymentType.Cash)
                .WithMessage(BusinessErrorMessage.OrderCashNotAllowedOnCardOrder)
                .MustAsync(async (orderId, ct) => (await LoadAsync(orderId, ct))?.PaymentStatus != PaymentStatus.Paid)
                .WithMessage(BusinessErrorMessage.OrderCashAlreadyCollected)
                .MustAsync(async (orderId, ct) =>
                    (await LoadAsync(orderId, ct))?.PaymentStatus is PaymentStatus.Pending or PaymentStatus.Failed)
                .WithMessage(BusinessErrorMessage.OrderPaymentNotOutstanding);

            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder);

            RuleFor(x => x.Amount)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0m)
                .WithMessage(BusinessErrorMessage.OrderCashAmountInvalid)
                .Must(amount => decimal.Round(amount, 2) == amount)
                .WithMessage(BusinessErrorMessage.OrderCashAmountInvalid);

            RuleFor(x => x.ReceivedAt)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must(receivedAt => receivedAt.ToUniversalTime() <= timeProvider.GetUtcNow().UtcDateTime)
                .WithMessage(BusinessErrorMessage.OrderCashReceivedAtInFuture)
                .MustAsync(NotBeforeTheCleanCouldBeginAsync)
                .WithMessage(BusinessErrorMessage.OrderCashReceivedAtBeforeClean);
        }

        private Task<Order?> LoadAsync(string orderId, CancellationToken cancellationToken) =>
            _orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        private async Task<bool> EmployeeIsAssignedToOrderAsync(
            Command command, string employeeId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order is null || order.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId);
        }

        // A cleaner may start the clean up to StartGraceWindowMinutes before its booked time, and the cash
        // is handed over at the door; a missing order is the OrderId rule's to report.
        private async Task<bool> NotBeforeTheCleanCouldBeginAsync(
            Command command, DateTime receivedAt, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order is null
                || receivedAt.ToUniversalTime()
                    >= order.CleaningDateTime.AddMinutes(-BookingPolicy.StartGraceWindowMinutes);
        }
    }

    public class Handler(IOrderRepository orderRepository, IPendingDispatch pending)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.Receipts)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            order.MarkCashCollected(command.EmployeeId, command.ReceivedAt.ToUniversalTime(), command.Amount);

            if (order.CurrentStatus == OrderStatus.Completed && order.Receipt is null)
            {
                pending.Enqueue(
                    QueueNames.GenerateReceipt,
                    new QueueEnvelope<GenerateReceiptMessage>(
                        MessageKeys.Receipt(order.Id),
                        order.TenantId,
                        new GenerateReceiptMessage(order.Id, Constants.Language.English)),
                    MessageKeys.Receipt(order.Id));
            }

            return BusinessResult.Success(new Response(order.Id, order.PaymentStatus));
        }
    }
}
