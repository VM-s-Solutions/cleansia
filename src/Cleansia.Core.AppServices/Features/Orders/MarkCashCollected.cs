using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The assigned cleaner records that they collected the cash owed for a CASH order. This flips the order
/// to <see cref="PaymentStatus.Paid"/> (the same terminal payment state a Stripe-charged card order
/// reaches) and stamps who/when. It is the gate that lets a cash order pass the CompleteOrder payment
/// check — a cash order cannot be completed until it is collected.
/// </summary>
public class MarkCashCollected
{
    public record Command(string OrderId) : ICommand<Response>;

    public record Response(string OrderId, PaymentStatus PaymentStatus);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IOrderAccessService _orderAccessService;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository,
            IOrderAccessService orderAccessService)
        {
            _orderRepository = orderRepository;
            _employeeRepository = employeeRepository;
            _orderAccessService = orderAccessService;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound)
                .MustAsync(OrderIsCashPaymentAsync)
                .WithMessage(BusinessErrorMessage.OrderNotCashPayment)
                .MustAsync(OrderIsNotAlreadyPaidAsync)
                .WithMessage(BusinessErrorMessage.OrderCashAlreadyCollected);

            // Same ownership gate as StartOrder / CompleteOrder: only an Approved cleaner assigned to the
            // order may collect its cash. Employee is server-derived from the caller (S1); empty caller
            // fails closed (S3 ownership).
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(EmployeeIsApprovedAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotApproved)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder);
        }

        private async Task<bool> OrderIsCashPaymentAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            return order?.PaymentType == PaymentType.Cash;
        }

        private async Task<bool> OrderIsNotAlreadyPaidAsync(string orderId, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            return order is not null && order.PaymentStatus != PaymentStatus.Paid;
        }

        private async Task<bool> EmployeeIsApprovedAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
            return employee?.ContractStatus == ContractStatus.Approved;
        }

        private async Task<bool> EmployeeIsAssignedToOrderAsync(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId)) return false;

            var order = await _orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId) ?? false;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // The validator guarantees an Approved, assigned caller, so the employee id is present.
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            order.MarkCashCollected(employeeId!);

            return BusinessResult.Success(new Response(order.Id, order.PaymentStatus));
        }
    }
}
