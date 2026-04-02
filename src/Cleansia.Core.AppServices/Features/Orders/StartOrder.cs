using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class StartOrder
{
    public record Command(
        string OrderId,
        string EmployeeId) : ICommand<Response>;

    public record Response(
        string OrderId,
        OrderStatus NewStatus);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeeRepository _employeeRepository;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository)
        {
            _orderRepository = orderRepository;
            _employeeRepository = employeeRepository;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(OrderIsConfirmedAsync)
                .WithMessage(BusinessErrorMessage.OrderNotConfirmed);

            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder);

            RuleFor(x => x.EmployeeId)
                .MustAsync(EmployeeHasNoOrderInProgressAsync)
                .WithMessage(BusinessErrorMessage.EmployeeAlreadyHasOrderInProgress);
        }

        private async Task<bool> EmployeeHasNoOrderInProgressAsync(string employeeId, CancellationToken cancellationToken)
        {
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
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.AssignedEmployees.Any(oe => oe.EmployeeId == command.EmployeeId) ?? false;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IEmailService emailService)
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

            // Send status update email
            try
            {
                var languageCode = order.User?.PreferredLanguageCode ?? "en";
                await emailService.SendOrderStatusUpdateEmailAsync(
                    order.CustomerEmail, order, "Started", languageCode, cancellationToken);
            }
            catch { /* Don't fail order start if email fails */ }

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                NewStatus: OrderStatus.InProgress
            ));
        }
    }
}
