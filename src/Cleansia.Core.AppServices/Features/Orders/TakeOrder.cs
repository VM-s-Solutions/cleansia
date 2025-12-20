using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
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
                .MustAsync(NotAlreadyAssignedToEmployeeAsync)
                .WithMessage(BusinessErrorMessage.EmployeeAlreadyAssignedToOrder);
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

            return !string.IsNullOrWhiteSpace(employee.ICO) &&
                   employee.Address is not null &&
                   employee.Availability.Any();
        }

        private async Task<bool> HasUploadedDocumentsAsync(string employeeId, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);

            if (employee == null) return false;

            return employee.ContractStatus != Domain.Enums.ContractStatus.Pending;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IEmployeeRepository employeeRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetByIdAsync(command.OrderId, cancellationToken);

            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            order.AssignEmployee(command.EmployeeId);

            var orderEmployee = OrderEmployee.Create(order, employee!);
            order.AddAssignedEmployee(orderEmployee);

            return BusinessResult.Success(new Response(order.Id, command.EmployeeId));
        }
    }
}
