using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The standalone acceptance, for a cleaner an administrator placed on a crew: the take accepts inline,
/// this is the same act for a seat formed without one. Any not-over order is admitted — a cleaner placed
/// on an in-progress job must be able to accept before completing. A seat that already has its row
/// answers success with that row and writes nothing (the double tap); a concurrent double tap is
/// arbitrated by the unique index on the seat at commit.
/// </summary>
public class AcceptWorkContract
{
    public record Command(string OrderId, string? AcceptedWorkContractTextId) : ICommand<Response>;

    public record Response(string OrderId, string AcceptanceId, DateTimeOffset AcceptedOn, string DocumentVersion);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderAccessService _orderAccessService;
        private readonly ILegalDocumentRepository _legalDocumentRepository;

        public Validator(
            IOrderRepository orderRepository,
            IOrderAccessService orderAccessService,
            ILegalDocumentRepository legalDocumentRepository)
        {
            _orderRepository = orderRepository;
            _orderAccessService = orderAccessService;
            _legalDocumentRepository = legalDocumentRepository;

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(command => !string.IsNullOrWhiteSpace(command.OrderId))
                .WithMessage(BusinessErrorMessage.Required)
                .Must(command => !string.IsNullOrWhiteSpace(command.AcceptedWorkContractTextId))
                .WithMessage(BusinessErrorMessage.WorkContractNotAccepted)
                .MustAsync((command, ct) => _orderRepository.ExistsAsync(command.OrderId, ct))
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(CallerHoldsASeatAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder)
                .MustAsync(NotCancelledAsync)
                .WithMessage(BusinessErrorMessage.TakeOrderAlreadyCancelled)
                .MustAsync(NotCompletedAsync)
                .WithMessage(BusinessErrorMessage.TakeOrderAlreadyCompleted)
                .MustAsync(TextBelongsToOrderContractAsync)
                .WithMessage(BusinessErrorMessage.WorkContractTextMismatch);
        }

        private async Task<bool> CallerHoldsASeatAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            return await _orderRepository
                .GetQueryable()
                .Where(o => o.Id == command.OrderId)
                .AnyAsync(o => o.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId), cancellationToken);
        }

        private async Task<bool> NotCancelledAsync(Command command, CancellationToken cancellationToken) =>
            await CurrentStatusAsync(command.OrderId, cancellationToken) != OrderStatus.Cancelled;

        private async Task<bool> NotCompletedAsync(Command command, CancellationToken cancellationToken) =>
            await CurrentStatusAsync(command.OrderId, cancellationToken) != OrderStatus.Completed;

        private Task<OrderStatus> CurrentStatusAsync(string orderId, CancellationToken cancellationToken) =>
            _orderRepository
                .GetQueryable()
                .Where(o => o.Id == orderId)
                .Select(o => o.CurrentStatus)
                .FirstAsync(cancellationToken);

        private async Task<bool> TextBelongsToOrderContractAsync(Command command, CancellationToken cancellationToken)
        {
            var orderDocumentId = await _orderRepository
                .GetQueryable()
                .Where(o => o.Id == command.OrderId)
                .Select(o => o.WorkContractDocumentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (orderDocumentId is null)
            {
                return false;
            }

            var document = await _legalDocumentRepository.GetByTextIdWithTextsAsync(
                command.AcceptedWorkContractTextId!, cancellationToken);

            return document?.Id == orderDocumentId;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService,
        IWorkContractAcceptanceRepository acceptanceRepository,
        IWorkContractAcceptor workContractAcceptor) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstAsync(o => o.Id == command.OrderId, cancellationToken);

            var seat = order.AssignedEmployees.First(oe => oe.EmployeeId == employeeId);

            var existing = await acceptanceRepository
                .GetQueryable()
                .Where(a => a.OrderEmployeeId == seat.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                return BusinessResult.Success(new Response(order.Id, existing.Id, existing.AcceptedOn, existing.DocumentVersion));
            }

            var acceptance = await workContractAcceptor.StageAsync(
                order, seat, command.AcceptedWorkContractTextId!, cancellationToken);

            return BusinessResult.Success(new Response(order.Id, acceptance.Id, acceptance.AcceptedOn, acceptance.DocumentVersion));
        }
    }
}
