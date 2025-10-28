using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class CompleteOrder
{
    public record Command(
        string OrderId,
        string EmployeeId,
        int ActualCompletionTimeMinutes,
        string CompletionNotes) : ICommand<Response>;

    public record Response(
        string OrderId,
        OrderStatus NewStatus,
        int ActualCompletionTime);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository)
        {
            _orderRepository = orderRepository;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(OrderIsInProgressAsync)
                .WithMessage(BusinessErrorMessage.OrderNotInProgress);

            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.ActualCompletionTimeMinutes)
                .GreaterThan(0)
                .WithMessage(BusinessErrorMessage.ActualTimeMustBePositive);

            RuleFor(x => x)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder);

            RuleFor(x => x.CompletionNotes)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.CompletionNotesRequired)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.CompletionNotesTooLong);
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
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.AssignedEmployees.Any(oe => oe.EmployeeId == command.EmployeeId) ?? false;
        }
    }

    public class Handler(
        IOrderRepository orderRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            order.CompleteOrder(command.ActualCompletionTimeMinutes, command.CompletionNotes);

            var completedStatusTrack = OrderStatusTrack.Create(OrderStatus.Completed, order);
            order.AddOrderStatus(completedStatusTrack);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                NewStatus: OrderStatus.Completed,
                ActualCompletionTime: command.ActualCompletionTimeMinutes
            ));
        }
    }
}
