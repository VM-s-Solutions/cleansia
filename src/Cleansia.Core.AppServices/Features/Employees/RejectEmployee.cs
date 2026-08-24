using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

[AuditAction("employee.reject", ResourceType = "User")]
public class RejectEmployee
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(IEmployeeRepository employeeRepository)
        {
            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.EmployeeId)
                .MustAsync(async (employeeId, cancellationToken) =>
                {
                    var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken);
                    return employee?.ContractStatus != ContractStatus.Rejected;
                })
                .WithMessage(BusinessErrorMessage.EmployeeAlreadyRejected)
                .When(x => !string.IsNullOrEmpty(x.EmployeeId));

            When(x => !string.IsNullOrEmpty(x.Reason), () =>
            {
                RuleFor(x => x.Reason)
                    .MaximumLength(500).WithMessage(BusinessErrorMessage.MaxLength);
            });
        }
    }

    public record Request(string? Reason);

    public record Command(string EmployeeId, string? Reason = null) : ICommand<Response>;

    public record Response(string EmployeeId, DateTimeOffset RejectedAt);

    // Keyed on the USER id (the audited subject the employee drill-in filters on), never the Employee
    // id. The admin's free-text reason is excluded — it could carry subject PII.
    public record ContractSnapshot(string UserId, string EmployeeId, ContractStatus Status);

    public class Handler(
        IEmployeeRepository employeeRepository,
        IUserRepository userRepository,
        IOrderRepository orderRepository,
        INotificationProducer notificationProducer,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail();
            var adminUser = await userRepository.GetByEmailAsync(adminEmail!, cancellationToken);

            if (adminUser is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    "Authentication",
                    BusinessErrorMessage.UserNotFound));
            }

            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(Command.EmployeeId),
                    BusinessErrorMessage.EmployeeNotFound));
            }

            var statusBefore = employee.ContractStatus;

            employee.Reject(adminUser.Id, command.Reason);

            await ReleaseFutureSeatsAsync(employee.Id, cancellationToken);

            auditContext.RecordChange(
                "User",
                employee.UserId,
                new ContractSnapshot(employee.UserId, employee.Id, statusBefore),
                new ContractSnapshot(employee.UserId, employee.Id, employee.ContractStatus));

            return BusinessResult.Success(new Response(employee.Id, employee.RejectedAt!.Value));
        }

        /// <summary>
        /// Rejecting a cleaner has to take their SEATS back, not just their contract.
        ///
        /// <para>Nothing anywhere filters <c>AssignedEmployees</c> by contract status. Offerability
        /// counts the row — <c>OrderSpecification.cs:141</c> admits an order only while
        /// <c>AssignedEmployees.Count &lt; MaxEmployees</c>, and <c>OrderVisibility.cs:55</c> treats any
        /// assignment as taken — while <c>TakeOrder</c>, <c>StartOrder</c>, <c>CompleteOrder</c> and
        /// <c>MarkCashCollected</c> every one require <c>ContractStatus.Approved</c>. So before this,
        /// a rejected cleaner's row held the job off the board AND could not be worked by the only
        /// person holding it. The order stranded: un-takeable and un-startable, until someone noticed.
        /// <c>Order.UnassignEmployee</c> hard-deletes the row, which is what genuinely returns the seat
        /// to the pool.</para>
        ///
        /// <para><b>Future <c>Confirmed</c> work only.</b> An order already <c>OnTheWay</c> or
        /// <c>InProgress</c> is a cleaner standing in a customer's home; taking that seat mid-clean is
        /// worse than letting an admin resolve it. Those are logged for the admin instead.</para>
        ///
        /// <para>The cleaner is told per assignment, through the same seam the admin reassign uses, so
        /// the subject is <c>AssignmentNotificationSubject.For(orderId, assignmentId)</c> and N released
        /// orders mint N distinct outbox keys rather than colliding on the cleaner.</para>
        /// </summary>
        private async Task ReleaseFutureSeatsAsync(string employeeId, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var held = await orderRepository.GetFutureConfirmedOrdersForEmployeeAsync(
                employeeId, now, cancellationToken);

            foreach (var order in held)
            {
                var assignment = order.AssignedEmployees
                    .FirstOrDefault(a => a.EmployeeId == employeeId);
                if (assignment is null)
                {
                    continue;
                }

                // Captured before UnassignEmployee hard-deletes the row — the notice is about THIS
                // assignment, and its id is the only thing separating it from any other the cleaner
                // held on the same order.
                var assignmentId = assignment.Id;
                var cleaner = assignment.Employee;

                order.UnassignEmployee(employeeId);

                if (cleaner is not null)
                {
                    await OrderAssignmentChangeNotifier.NotifyCleanerOfRevocationAsync(
                        order, cleaner, assignmentId, notificationProducer, cancellationToken);
                }
            }
        }
    }
}
