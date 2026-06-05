using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

public class StartOrder
{
    public record Command(string OrderId) : ICommand<Response>;

    public record Response(
        string OrderId,
        OrderStatus NewStatus);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IOrderAccessService _orderAccessService;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository,
            IOrderAccessService orderAccessService)
        {
            _orderRepository = orderRepository;
            _employeeRepository = employeeRepository;
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
                .MustAsync(EmployeeIsApprovedAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotApproved)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder)
                .MustAsync(EmployeeHasNoOrderInProgressAsync)
                .WithMessage(BusinessErrorMessage.EmployeeAlreadyHasOrderInProgress);
        }

        // T-0109 (EMP-GAP-01): StartOrder previously had NO ContractStatus gate, so
        // a rejected cleaner already assigned to a Confirmed order could start it.
        // Same honest == Approved rule used by TakeOrder / CompleteOrder. Employee is
        // server-derived from the caller (S1 server-truth); empty caller fails closed.
        private async Task<bool> EmployeeIsApprovedAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
            return employee?.ContractStatus == ContractStatus.Approved;
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

            return currentStatus is OrderStatus.Confirmed or OrderStatus.OnTheWay;
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

        private async Task<bool> EmployeeHasNoOrderInProgressAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var hasInProgressOrder = await _orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                .Where(o => o.AssignedEmployees.Any(ae => ae.EmployeeId == employeeId))
                .AnyAsync(o => o.OrderStatusHistory
                    .OrderByDescending(h => h.CreatedOn)
                    .FirstOrDefault()!.Status == OrderStatus.InProgress, cancellationToken);

            return !hasInProgressOrder;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IEmailService emailService,
        IPendingDispatch pending,
        ILogger<Handler> logger)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.Currency)
                .Include(o => o.CustomerAddress)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            order!.StartOrder();

            var statusTrack = OrderStatusTrack.Create(OrderStatus.InProgress, order);
            order.AddOrderStatus(statusTrack);

            try
            {
                var languageCode = order.User?.PreferredLanguageCode ?? Constants.Language.English;
                await emailService.SendOrderStatusUpdateEmailAsync(
                    order.CustomerEmail, order, "Started", languageCode, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send order Started email for order {OrderId}", order.Id);
            }

            if (!string.IsNullOrEmpty(order.UserId))
            {
                pending.Enqueue(
                    QueueNames.NotificationsDispatch,
                    new QueueEnvelope<SendPushNotificationMessage>(
                        MessageKeys.Push(order.UserId, NotificationEventCatalog.OrderInProgress, order.Id),
                        order.TenantId,
                        new SendPushNotificationMessage(
                            UserId: order.UserId,
                            EventKey: NotificationEventCatalog.OrderInProgress,
                            Args: new Dictionary<string, string>
                            {
                                ["orderId"] = order.Id,
                                ["orderNumber"] = order.DisplayOrderNumber,
                            },
                            TenantId: order.TenantId)),
                    MessageKeys.Push(order.UserId, NotificationEventCatalog.OrderInProgress, order.Id));
            }

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                NewStatus: OrderStatus.InProgress
            ));
        }
    }
}
