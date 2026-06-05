using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Cleaner taps "On my way" — appends an <see cref="OrderStatus.OnTheWay"/>
/// track and fires <c>order.on_the_way</c> to the customer. Optional step
/// between Confirmed and InProgress; <see cref="StartOrder"/> accepts either
/// as a prior state. Re-tap from OnTheWay is rejected to avoid double pushes.
/// </summary>
public class NotifyOnTheWay
{
    public record Command(string OrderId) : ICommand<Response>;

    public record Response(
        string OrderId,
        OrderStatus NewStatus);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderAccessService _orderAccessService;

        public Validator(
            IOrderRepository orderRepository,
            IOrderAccessService orderAccessService)
        {
            _orderRepository = orderRepository;
            _orderAccessService = orderAccessService;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(OrderIsConfirmedAsync)
                .WithMessage(BusinessErrorMessage.OrderNotConfirmed);

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder);
        }

        private async Task<bool> OrderIsConfirmedAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order == null) return false;

            var currentStatus = order.OrderStatusHistory
                .OrderByDescending(osh => osh.CreatedOn)
                .FirstOrDefault()?.Status;

            return currentStatus == OrderStatus.Confirmed;
        }

        private async Task<bool> EmployeeIsAssignedToOrderAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId) ?? false;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IPendingDispatch pending)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            order!.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.OnTheWay, order));

            if (!string.IsNullOrEmpty(order.UserId))
            {
                pending.Enqueue(
                    QueueNames.NotificationsDispatch,
                    new QueueEnvelope<SendPushNotificationMessage>(
                        MessageKeys.Push(order.UserId, NotificationEventCatalog.OrderOnTheWay, order.Id),
                        order.TenantId,
                        new SendPushNotificationMessage(
                            UserId: order.UserId,
                            EventKey: NotificationEventCatalog.OrderOnTheWay,
                            Args: new Dictionary<string, string>
                            {
                                ["orderId"] = order.Id,
                                ["orderNumber"] = order.DisplayOrderNumber,
                            },
                            TenantId: order.TenantId)),
                    MessageKeys.Push(order.UserId, NotificationEventCatalog.OrderOnTheWay, order.Id));
            }

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                NewStatus: OrderStatus.OnTheWay));
        }
    }
}
