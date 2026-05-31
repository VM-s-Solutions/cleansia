using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class DeleteOrderIssue
{
    public record Command(string OrderId, string IssueId) : ICommand<Response>;

    public record Response(bool Success);

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

            RuleFor(x => x.IssueId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
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
                    new Error(nameof(command.IssueId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .Include(o => o.OrderIssues)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null || !order.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.OrderId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            var issue = order.OrderIssues.FirstOrDefault(i =>
                i.Id == command.IssueId && i.ReportedByEmployeeId == employeeId);

            if (issue == null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.IssueId), BusinessErrorMessage.NotFound));
            }

            order.RemoveIssue(issue);

            return BusinessResult.Success(new Response(Success: true));
        }
    }
}
