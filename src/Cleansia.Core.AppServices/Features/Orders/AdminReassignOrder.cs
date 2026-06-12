using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class AdminReassignOrder
{
    public record Command(
        string OrderId,
        // The assignment to replace. Null = a pure add into an open spot (no cleaner removed).
        string? FromEmployeeId,
        string ToEmployeeId
    ) : ICommand<Response>;

    public record Response(
        string OrderId,
        string ToEmployeeId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.ToEmployeeId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IEmployeeRepository employeeRepository,
        IUserSessionProvider userSessionProvider
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            _ = userSessionProvider.GetUserId()!;
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId),
                    BusinessErrorMessage.OrderNotFound));
            }

            var target = await employeeRepository.GetByIdAsync(command.ToEmployeeId, cancellationToken);
            if (target == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ToEmployeeId),
                    BusinessErrorMessage.EmployeeNotFound));
            }

            if (order.AssignedEmployees.Any(oe => oe.EmployeeId == command.ToEmployeeId))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ToEmployeeId),
                    BusinessErrorMessage.EmployeeAlreadyAssignedToOrder));
            }

            if (!string.IsNullOrEmpty(command.FromEmployeeId))
            {
                if (order.AssignedEmployees.All(oe => oe.EmployeeId != command.FromEmployeeId))
                {
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.FromEmployeeId),
                        BusinessErrorMessage.EmployeeNotAssignedToOrder));
                }

                order.UnassignEmployee(command.FromEmployeeId);
            }

            // Spot ceiling is checked here so exceeding MaxEmployees is a business error, not the
            // InvalidOperationException AddAssignedEmployee throws (MaxEmployees / AvailableSpots).
            if (!order.HasAvailableSpots)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.ToEmployeeId),
                    BusinessErrorMessage.NoAvailableSpots));
            }

            order.AddAssignedEmployee(OrderEmployee.Create(order, target));

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                ToEmployeeId: command.ToEmployeeId));
        }
    }
}
