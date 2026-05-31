using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Mappers;
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

public class TakeOrder
{
    public record Command(string OrderId) : ICommand<Response>;

    public record Response(string OrderId, string EmployeeId);

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
                .MustAsync(HasAvailableSpotsAsync)
                .WithMessage(BusinessErrorMessage.NoAvailableSpots);

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(CallerIsEmployeeAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound)
                .MustAsync(HasCompletedProfileAsync)
                .WithMessage(BusinessErrorMessage.EmployeeProfileIncomplete)
                .MustAsync(HasUploadedDocumentsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeDocumentsMissing)
                .MustAsync(NotAlreadyAssignedToEmployeeAsync)
                .WithMessage(BusinessErrorMessage.EmployeeAlreadyAssignedToOrder)
                .MustAsync(NotExceedWeeklyOrderLimitAsync)
                .WithMessage(BusinessErrorMessage.WeeklyOrderLimitReached)
                .MustAsync(NotHaveTimeConflictAsync)
                .WithMessage(BusinessErrorMessage.TimeConflict);
        }

        private async Task<bool> HasAvailableSpotsAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            return order?.HasAvailableSpots ?? false;
        }

        private async Task<bool> CallerIsEmployeeAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            return !string.IsNullOrEmpty(employeeId);
        }

        private async Task<bool> NotAlreadyAssignedToEmployeeAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return true;

            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.AssignedEmployees.All(oe => oe.EmployeeId != employeeId) ?? true;
        }

        private async Task<bool> HasCompletedProfileAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var employee = await _employeeRepository
                .GetQueryable()
                .Include(e => e.Address)
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

            // Availability is no longer a gate (the weekly schedule
            // isn't read by matching/push today). A cleaner can take
            // orders once they have an address; documents + approval
            // are still enforced separately.
            return employee?.Address is not null;
        }

        private async Task<bool> HasUploadedDocumentsAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
            return employee?.ContractStatus != ContractStatus.Pending;
        }

        private async Task<bool> NotExceedWeeklyOrderLimitAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
            if (employee == null) return false;

            var weeklyCount = await _orderRepository.GetEmployeeOrderCountThisWeekAsync(employeeId, cancellationToken);

            var limit = employee.AverageRating switch
            {
                <= 3.5m => 3,
                <= 4.5m => 6,
                _ => 10
            };

            return weeklyCount < limit;
        }

        private async Task<bool> NotHaveTimeConflictAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var order = await _orderRepository
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null) return false;

            return !await _orderRepository.HasOverlappingOrderAsync(
                employeeId,
                order.CleaningDateTime,
                order.EstimatedTime,
                cancellationToken);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IEmployeeRepository employeeRepository,
        IOrderAccessService orderAccessService,
        IQueueClient queueClient,
        IEmailService emailService,
        ILogger<Handler> logger)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.Currency)
                .Include(o => o.CustomerAddress)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            var employee = await employeeRepository.GetByIdAsync(employeeId!, cancellationToken);

            var orderEmployee = OrderEmployee.Create(order!, employee!);
            order!.AddAssignedEmployee(orderEmployee);

            var statusChanged = false;
            var currentStatus = order.GetCurrentOrderStatus();
            if (currentStatus is OrderStatus.New or OrderStatus.Pending)
            {
                order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
                statusChanged = true;
            }

            if (statusChanged && !string.IsNullOrEmpty(order.UserId))
            {
                await queueClient.SendAsync(
                    QueueNames.NotificationsDispatch,
                    new SendPushNotificationMessage(
                        UserId: order.UserId,
                        EventKey: NotificationEventCatalog.OrderConfirmed,
                        Args: new Dictionary<string, string>
                        {
                            ["orderId"] = order.Id,
                            ["orderNumber"] = order.DisplayOrderNumber,
                        },
                        TenantId: order.TenantId),
                    cancellationToken);
            }

            if (statusChanged && !string.IsNullOrEmpty(order.CustomerEmail))
            {
                try
                {
                    var languageCode = order.User?.PreferredLanguageCode ?? Constants.Language.English;
                    await emailService.SendOrderStatusUpdateEmailAsync(
                        order.CustomerEmail, order, "Confirmed", languageCode, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send order Confirmed email for order {OrderId}", order.Id);
                }
            }

            return BusinessResult.Success(new Response(order.Id, employeeId!));
        }
    }
}
