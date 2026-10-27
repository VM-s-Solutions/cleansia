using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Specifications;
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
        IOrderAccessService orderAccessService,
        INotificationProducer notificationProducer) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);

            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // Before the crew is read, so two cleaners marking at once see each other's mark, and a mark that
            // races the job's completion reads it.
            await orderRepository.LockForCrewChangeAsync(command.OrderId, cancellationToken);

            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(oe => oe.Employee)
                    .ThenInclude(e => e!.User)
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

            var firstMark = seat.CompletionConfirmedAt is null;
            seat.MarkCompletionConfirmed(DateTime.UtcNow);

            if (firstMark)
            {
                foreach (var crewMate in order.AssignedEmployees.Where(oe => oe.Id != seat.Id))
                {
                    if (!JobReminderRecipient.IsEligible(crewMate.Employee))
                    {
                        continue;
                    }

                    await notificationProducer.NotifyAsync(
                        crewMate.Employee!.UserId,
                        NotificationEventCatalog.OrderCrewMateDone,
                        new Dictionary<string, string>
                        {
                            ["orderId"] = order.Id,
                            ["orderNumber"] = order.DisplayOrderNumber,
                        },
                        order.TenantId,
                        AssignmentNotificationSubject.For(order.Id, seat.Id),
                        cancellationToken);
                }
            }

            return BusinessResult.Success(new Response(
                order.Id,
                seat.CompletionConfirmedAt!.Value,
                order.EveryCleanerMarkedDone));
        }
    }
}
