using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class TakeOrder
{
    public record Command(string OrderId, string EmployeeId) : ICommand<Response>;

    public record Response(string OrderId, string EmployeeId);

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
                .MustAsync(HasAvailableSpotsAsync)
                .WithMessage(BusinessErrorMessage.NoAvailableSpots);

            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound)
                .MustAsync(HasCompletedProfileAsync)
                .WithMessage(BusinessErrorMessage.EmployeeProfileIncomplete)
                .MustAsync(HasUploadedDocumentsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeDocumentsMissing);

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
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

            if (order == null) return false;

            return order.HasAvailableSpots;
        }

        private async Task<bool> NotAlreadyAssignedToEmployeeAsync(Command command, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.AssignedEmployees.All(oe => oe.EmployeeId != command.EmployeeId) ?? true;
        }

        private async Task<bool> HasCompletedProfileAsync(string employeeId, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository
                .GetQueryable()
                .Include(e => e.Address)
                .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

            if (employee == null) return false;

            return employee.Address is not null &&
                   employee.Availability.Any();
        }

        private async Task<bool> HasUploadedDocumentsAsync(string employeeId, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);

            if (employee == null) return false;

            return employee.ContractStatus != Domain.Enums.ContractStatus.Pending;
        }

        private async Task<bool> NotExceedWeeklyOrderLimitAsync(Command command, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);
            if (employee == null) return false;

            var weeklyCount = await _orderRepository.GetEmployeeOrderCountThisWeekAsync(command.EmployeeId, cancellationToken);

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
            var order = await _orderRepository
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null) return false;

            return !await _orderRepository.HasOverlappingOrderAsync(
                command.EmployeeId,
                order.CleaningDateTime,
                order.EstimatedTime,
                cancellationToken);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IEmployeeRepository employeeRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            var orderEmployee = OrderEmployee.Create(order!, employee!);
            order!.AddAssignedEmployee(orderEmployee);

            var currentStatus = order.GetCurrentOrderStatus();
            if (currentStatus is OrderStatus.New or OrderStatus.Pending)
            {
                order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
            }

            return BusinessResult.Success(new Response(order.Id, command.EmployeeId));
        }
    }
}
