using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

public class CompleteOrder
{
    public record Command(
        string OrderId,
        int ActualCompletionTimeMinutes,
        string? CompletionNotes = null) : ICommand<Response>;

    public record Response(
        string OrderId,
        OrderStatus NewStatus,
        int ActualCompletionTime);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IOrderPhotoRepository _orderPhotoRepository;
        private readonly IOrderAccessService _orderAccessService;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository,
            IOrderPhotoRepository orderPhotoRepository,
            IOrderAccessService orderAccessService)
        {
            _orderRepository = orderRepository;
            _employeeRepository = employeeRepository;
            _orderPhotoRepository = orderPhotoRepository;
            _orderAccessService = orderAccessService;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(OrderIsInProgressAsync)
                .WithMessage(BusinessErrorMessage.OrderNotInProgress)
                .MustAsync(HasAfterPhotosAsync)
                .WithMessage(BusinessErrorMessage.AfterPhotosRequired);

            RuleFor(x => x.ActualCompletionTimeMinutes)
                .GreaterThan(0)
                .WithMessage(BusinessErrorMessage.ActualTimeMustBePositive);

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder)
                .MustAsync(HasCompletedProfileAsync)
                .WithMessage(BusinessErrorMessage.EmployeeProfileIncomplete)
                .MustAsync(HasUploadedDocumentsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeDocumentsMissing);

            When(x => !string.IsNullOrEmpty(x.CompletionNotes), () =>
            {
                RuleFor(x => x.CompletionNotes)
                    .MaximumLength(1000)
                    .WithMessage(BusinessErrorMessage.CompletionNotesTooLong);
            });
        }

        private async Task<bool> OrderIsInProgressAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order == null) return false;

            var currentStatus = order.OrderStatusHistory
                .OrderByDescending(osh => osh.CreatedOn)
                .FirstOrDefault()?.Status;

            return currentStatus == OrderStatus.InProgress;
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

        private async Task<bool> HasCompletedProfileAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var employee = await _employeeRepository
                .GetQueryable()
                .Include(e => e.Address)
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

            return employee?.Address is not null && (employee?.Availability.Any() ?? false);
        }

        private async Task<bool> HasUploadedDocumentsAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
            return employee?.ContractStatus != ContractStatus.Pending;
        }

        private async Task<bool> HasAfterPhotosAsync(string orderId, CancellationToken cancellationToken)
        {
            var photoCount = await _orderPhotoRepository
                .GetPhotoCountByOrderIdAndTypeAsync(orderId, PhotoType.After, cancellationToken);

            return photoCount > 0;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IQueueClient queueClient,
        IEmailService emailService,
        ILoyaltyService loyaltyService,
        IReferralService referralService,
        ILogger<Handler> logger)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.SelectedServices).ThenInclude(os => os.Service)
                .Include(o => o.SelectedPackages).ThenInclude(op => op.Package)
                .Include(o => o.CustomerAddress).ThenInclude(a => a!.Country)
                .Include(o => o.Currency)
                .Include(o => o.Receipt)
                .Include(o => o.User).ThenInclude(u => u!.PreferredLanguage)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            order.CompleteOrder(command.ActualCompletionTimeMinutes, command.CompletionNotes);

            var completedStatusTrack = OrderStatusTrack.Create(OrderStatus.Completed, order);
            order.AddOrderStatus(completedStatusTrack);

            if (order.Receipt is null)
            {
                var languageCode = order.User?.PreferredLanguageCode ?? Constants.Language.English;
                await queueClient.SendAsync(QueueNames.GenerateReceipt,
                    new GenerateReceiptMessage(order.Id, languageCode), cancellationToken);
            }

            // Push notification for the customer's "All done!" toast.
            // Skip for guest orders (no UserId → no device).
            if (!string.IsNullOrEmpty(order.UserId))
            {
                await queueClient.SendAsync(
                    QueueNames.NotificationsDispatch,
                    new SendPushNotificationMessage(
                        UserId: order.UserId,
                        EventKey: NotificationEventCatalog.OrderCompleted,
                        Args: new Dictionary<string, string>
                        {
                            ["orderId"] = order.Id,
                            ["orderNumber"] = order.DisplayOrderNumber,
                        },
                        TenantId: order.TenantId),
                    cancellationToken);
            }

            try
            {
                var languageCode = order.User?.PreferredLanguageCode ?? Constants.Language.English;
                await emailService.SendOrderStatusUpdateEmailAsync(
                    order.CustomerEmail, order, "Completed", languageCode, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send order Completed email for order {OrderId}", order.Id);
            }

            await loyaltyService.GrantForCompletedOrderAsync(order.Id, cancellationToken);

            await referralService.ProcessOrderCompletedAsync(order.Id, order.UserId, cancellationToken);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                NewStatus: OrderStatus.Completed,
                ActualCompletionTime: command.ActualCompletionTimeMinutes
            ));
        }
    }
}
