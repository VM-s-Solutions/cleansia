using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class UpdateOrderNote
{
    public record Command(
        string OrderId,
        string NoteId,
        string Content) : ICommand<Response>;

    public record Response(string NoteId, string Content);

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

            RuleFor(x => x.NoteId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.Content)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.OrderNoteContentRequired)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.NoteId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .Include(o => o.OrderNotes)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null || !order.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            // Only the author can edit their own note — the screen UX
            // hides the edit affordance on others' notes, but enforce
            // it at the API too in case the client is bypassed.
            var note = order.OrderNotes.FirstOrDefault(n =>
                n.Id == command.NoteId && n.EmployeeId == employeeId);

            if (note == null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.NoteId), BusinessErrorMessage.NotFound));
            }

            note.UpdateContent(command.Content);

            return BusinessResult.Success(new Response(NoteId: note.Id, Content: note.Content));
        }
    }
}
