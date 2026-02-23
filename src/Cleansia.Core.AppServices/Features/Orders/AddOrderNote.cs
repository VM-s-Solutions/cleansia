using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class AddOrderNote
{
    public record Command(
        string OrderId,
        string EmployeeId,
        string Content) : ICommand<Response>;

    public record Response(
        string NoteId,
        string Content,
        DateTimeOffset CreatedAt);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.Content)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x)
                .MustAsync(EmployeeIsAssignedAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder);
        }

        private async Task<bool> EmployeeIsAssignedAsync(Command command, CancellationToken cancellationToken)
        {
            return true; // Validated in handler
        }
    }

    public class Handler(
        IOrderRepository orderRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            if (!order.AssignedEmployees.Any(oe => oe.EmployeeId == command.EmployeeId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.EmployeeId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            var note = OrderNote.Create(command.OrderId, command.EmployeeId, command.Content);
            order.AddNote(note);

            return BusinessResult.Success(new Response(
                NoteId: note.Id,
                Content: note.Content,
                CreatedAt: DateTimeOffset.UtcNow));
        }
    }
}
