using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// A cleaner on a job with two or more cleaners marks their own part of it done; once everyone still on it
/// has, any of them can complete it.
/// </summary>
public class MarkMyPartDone
{
    public record Command(string OrderId) : ICommand<Response>;

    /// <param name="CompletionConfirmedAt">
    /// The stamp that was kept. The first mark wins, so marking again returns the first one's time, which is
    /// why this command needs no "already marked" error key.
    /// </param>
    /// <param name="CrewDone">Every cleaner still on the job has marked their part done.</param>
    public record Response(string OrderId, DateTime CompletionConfirmedAt, bool CrewDone);

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
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var seat = order.AssignedEmployees.FirstOrDefault(oe => oe.EmployeeId == employeeId);

            // Before the job's state, so a cleaner who is not on the crew learns nothing about the job.
            if (seat is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            if (order.CurrentStatus != OrderStatus.InProgress)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotInProgress));
            }

            seat.MarkCompletionConfirmed(DateTime.UtcNow);

            return BusinessResult.Success(new Response(
                order.Id,
                seat.CompletionConfirmedAt!.Value,
                order.AssignedEmployees.All(oe => oe.CompletionConfirmedAt is not null)));
        }
    }
}
