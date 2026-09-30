using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

// The command carries three ids, so without a named resource the resolver records none of them.
[AuditAction("order.reassign", ResourceType = "Order")]
public class AdminReassignOrder
{
    public record Command(
        string OrderId,
        // The assignment to replace. Null = a pure add into an open spot (no cleaner removed).
        string? FromEmployeeId,
        string ToEmployeeId,
        // Required when someone is removed, and told to them: a placement is an offer the admin may
        // withdraw, never silently (owner ruling 2026-09-28).
        string? RemovalReason = null
    ) : ICommand<Response>;

    /// <summary>Who held a seat before and after; the removal reason rides on the audit row's own column.</summary>
    public record CrewSnapshot(string OrderId, string? EmployeeId);

    public record Response(
        string OrderId,
        string ToEmployeeId);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ILegalDocumentResolver _legalDocumentResolver;
        private readonly IUserConsentRepository _userConsentRepository;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository,
            ILegalDocumentResolver legalDocumentResolver,
            IUserConsentRepository userConsentRepository)
        {
            _orderRepository = orderRepository;
            _employeeRepository = employeeRepository;
            _legalDocumentResolver = legalDocumentResolver;
            _userConsentRepository = userConsentRepository;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.ToEmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(TargetExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound)
                .MustAsync(TargetIsApprovedAsync)
                .WithMessage(BusinessErrorMessage.ReassignEmployeeNotApproved)
                .MustAsync(TargetHoldsCurrentCleanerDocumentsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeLegalDocumentsNotAccepted)
                .MustAsync(TargetWorksInTheOrdersMarketAsync)
                .WithMessage(BusinessErrorMessage.ReassignEmployeeOtherMarket)
                .MustAsync(TargetIsFreeAtTheCleaningTimeAsync)
                .WithMessage(BusinessErrorMessage.ReassignEmployeeBusy);

            RuleFor(x => x.RemovalReason)
                .Cascade(CascadeMode.Stop)
                .Must(reason => !string.IsNullOrWhiteSpace(reason))
                .WithMessage(BusinessErrorMessage.OrderRemovalReasonRequired)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .When(x => !string.IsNullOrEmpty(x.FromEmployeeId));
        }

        private async Task<bool> TargetExistsAsync(string employeeId, CancellationToken cancellationToken) =>
            await _employeeRepository.GetByIdAsync(employeeId, cancellationToken) is not null;

        // Deactivated(...) leaves ContractStatus untouched, so a departed or erased cleaner still reads Approved.
        private async Task<bool> TargetIsApprovedAsync(string employeeId, CancellationToken cancellationToken) =>
            await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
                is { IsActive: true, ContractStatus: ContractStatus.Approved };

        // The take's own gate: an admin placing a cleaner is the cleaner taking the job, so the contract
        // versions in force must be the ones they accepted.
        private async Task<bool> TargetHoldsCurrentCleanerDocumentsAsync(string employeeId, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
            return employee is not null && await CleanerLegalDocuments.AllAcceptedAsync(
                _legalDocumentResolver, _userConsentRepository, employee.UserId, employee.WorkCountryId, cancellationToken);
        }

        private async Task<bool> TargetWorksInTheOrdersMarketAsync(
            Command command, string employeeId, CancellationToken cancellationToken)
        {
            var market = await _orderRepository
                .GetQueryable()
                .Where(o => o.Id == command.OrderId)
                .Select(o => new { CountryId = o.CustomerAddress != null ? o.CustomerAddress.CountryId : null })
                .FirstOrDefaultAsync(cancellationToken);
            if (market is null)
            {
                return true;
            }

            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
            return !string.IsNullOrEmpty(employee?.WorkCountryId)
                && string.Equals(employee.WorkCountryId, market.CountryId, StringComparison.Ordinal);
        }

        // A cleaner already on this order is refused by the handler as already assigned, not as busy.
        private async Task<bool> TargetIsFreeAtTheCleaningTimeAsync(
            Command command, string employeeId, CancellationToken cancellationToken)
        {
            var slot = await _orderRepository
                .GetQueryable()
                .Where(o => o.Id == command.OrderId)
                .Select(o => new
                {
                    o.CleaningDateTime,
                    o.EstimatedTime,
                    AlreadyOnIt = o.AssignedEmployees.Any(ae => ae.EmployeeId == employeeId),
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (slot is null || slot.AlreadyOnIt)
            {
                return true;
            }

            return !await _orderRepository.HasOverlappingOrderAsync(
                employeeId, slot.CleaningDateTime, slot.EstimatedTime, cancellationToken);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IEmployeeRepository employeeRepository,
        IUserSessionProvider userSessionProvider,
        INotificationProducer notificationProducer,
        IAuditContext auditContext
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            _ = userSessionProvider.GetUserId()!;
            // OrderStatusHistory is not optional — the Confirmed append below derives its Sequence from
            // the loaded history, and without it the row takes the creation row's place.
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees)
                .AsSplitQuery()
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

            Employee? removed = null;
            string? removedAssignmentId = null;
            if (!string.IsNullOrEmpty(command.FromEmployeeId))
            {
                var existing = order.AssignedEmployees
                    .FirstOrDefault(oe => oe.EmployeeId == command.FromEmployeeId);
                if (existing is null)
                {
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.FromEmployeeId),
                        BusinessErrorMessage.EmployeeNotAssignedToOrder));
                }

                // Captured BEFORE UnassignEmployee hard-deletes the row: the revocation message is about
                // this assignment, and its id is the only thing that distinguishes it from the cleaner's
                // previous or next assignment on the same order.
                removedAssignmentId = existing.Id;

                // Read by id rather than off the assignment's navigation: this handler's query does not
                // include Employee, and a null navigation would silently drop the notice.
                removed = await employeeRepository.GetByIdAsync(command.FromEmployeeId, cancellationToken);
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

            var assignment = OrderEmployee.Create(order, target);
            order.AddAssignedEmployee(assignment);

            // An admin assigning a cleaner IS a cleaner taking the job, so the fulfilment axis moves —
            // mirroring TakeOrder, and guarded the same way so a reassignment on an OnTheWay or
            // InProgress order never walks the status backwards.
            //
            // The crew is the fact and the status is its summary: a paid card order rests at New until
            // somebody takes it, and an order that lost its last cleaner is walked back to New by the
            // release. Without this line an admin-assigned order would sit at New with a crew on it,
            // and the sweeps that select Confirmed — the pre-cleaning reminder, the cleaner job
            // reminder, the tomorrow digest, NotifyOnTheWay and StartOrder — would not see it. Each of
            // them still reads AssignedEmployees as well, because that is the fact the summary
            // follows, not a belt for it.
            if (order.GetCurrentOrderStatus() is OrderStatus.New)
            {
                order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
            }

            await OrderCleanerAssignedNotifier.NotifyCustomerOfAssignmentAsync(
                order, assignment, notificationProducer, cancellationToken);

            await OrderAssignmentChangeNotifier.NotifyCleanerOfAssignmentAsync(
                order, target, assignment.Id, notificationProducer, cancellationToken);

            var removalReason = string.IsNullOrEmpty(command.FromEmployeeId) ? null : command.RemovalReason?.Trim();
            if (removed is not null)
            {
                // The REMOVED assignment's own id, captured before UnassignEmployee deleted the row —
                // the revocation is about that assignment, not about the one replacing it.
                await OrderAssignmentChangeNotifier.NotifyCleanerOfRevocationAsync(
                    order, removed, removedAssignmentId!, notificationProducer, cancellationToken);
            }

            auditContext.RecordChange(
                "Order",
                order.Id,
                new CrewSnapshot(order.Id, command.FromEmployeeId),
                new CrewSnapshot(order.Id, command.ToEmployeeId),
                removalReason);

            return BusinessResult.Success(new Response(
                OrderId: order.Id,
                ToEmployeeId: command.ToEmployeeId));
        }
    }
}
