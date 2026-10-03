using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

/// <summary>
/// A cleaner's pay on one order: the job on an order they completed, and on a cancelled one their share of
/// the late-cancellation or lockout fee the company collected, never of a fee still owed (owner ruling
/// 2026-09-28, decision 12).
/// </summary>
public class CalculateOrderPay
{
    public record Command(string OrderId, string EmployeeId) : ICommand<Response>;

    public record Response(string EmployeePayrollId);

    /// <summary>Asks for the pay of every cleaner on the order, off the caller's path.</summary>
    public static void EnqueueForCrew(Order order, IPendingDispatch pending)
    {
        foreach (var assignment in order.AssignedEmployees)
        {
            var key = MessageKeys.Pay(order.Id, assignment.EmployeeId);
            pending.Enqueue(
                QueueNames.CalculateOrderPay,
                new QueueEnvelope<CalculateOrderPayMessage>(
                    key, order.TenantId, new CalculateOrderPayMessage(order.Id, assignment.EmployeeId)),
                key);
        }
    }

    /// <summary>
    /// The fee a cancelled order has actually brought in: what its payment kept at cancellation, or, on one
    /// that took no payment, the fee receivables paid since. Zero on a cancellation that charged no fee.
    /// </summary>
    private static async Task<decimal> CollectedFeeAsync(
        Order order,
        IReceivableRepository receivableRepository,
        IRefundRepository refundRepository,
        ICreditAccountRepository creditAccountRepository,
        CancellationToken cancellationToken)
    {
        if (order.CancellationFeeRate is not > 0m)
        {
            return 0m;
        }

        if (!order.TookNoPayment)
        {
            // Never more than the company still holds. An order refunded before it was cancelled or locked
            // out is no longer Paid, so the cancellation refunds nothing and the price less the recorded
            // refund would count money already given back. A cancellation refund still waiting for its
            // re-drive is not among the succeeded refunds yet, so the first term keeps the fee to its size.
            var stillHeld = order.TotalPrice
                - await refundRepository.GetSucceededRefundTotalForOrderAsync(order.Id, cancellationToken)
                - await creditAccountRepository.GetReturnedTotalForOrderAsync(order.Id, cancellationToken);
            return Math.Max(0m, Math.Min(order.TotalPrice - (order.CancellationRefundAmount ?? 0m), stillHeld));
        }

        return await receivableRepository
            .GetAll()
            .Where(r => r.OrderId == order.Id
                && r.Status == ReceivableStatus.Paid
                && (r.Kind == ReceivableKind.CashCancellationFee || r.Kind == ReceivableKind.Lockout))
            .SumAsync(r => r.Amount, cancellationToken);
    }

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeePayConfigRepository _payConfigRepository;
        private readonly IOrderEmployeePayRepository _orderEmployeePayRepository;
        private readonly IReceivableRepository _receivableRepository;
        private readonly IRefundRepository _refundRepository;
        private readonly ICreditAccountRepository _creditAccountRepository;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository,
            IPayPeriodRepository payPeriodRepository,
            IEmployeePayConfigRepository payConfigRepository,
            IOrderEmployeePayRepository orderEmployeePayRepository,
            IReceivableRepository receivableRepository,
            IRefundRepository refundRepository,
            ICreditAccountRepository creditAccountRepository)
        {
            _orderRepository = orderRepository;
            _payConfigRepository = payConfigRepository;
            _orderEmployeePayRepository = orderEmployeePayRepository;
            _receivableRepository = receivableRepository;
            _refundRepository = refundRepository;
            _creditAccountRepository = creditAccountRepository;

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

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssigned);

            // Negate: Must passes when predicate is true. We want to REJECT
            // when a pay row already exists for this (OrderId, EmployeeId)
            // pair — so the predicate must return true when NO row exists.
            // The repo method returns true when a row exists, hence the !.
            RuleFor(x => x)
                .MustAsync(async (cmd, ct) => !await ExistsWithOrderIdAndEmployeeIdAsync(cmd, ct))
                .WithMessage(BusinessErrorMessage.PayAlreadyCalculated);

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync((_, ct) => payPeriodRepository.ExistsActivePeriodAsync(ct))
                .WithMessage(BusinessErrorMessage.NoActivePeriod);

            RuleFor(x => x)
                .MustAsync(ConfigsExistAsync)
                .WithMessage(BusinessErrorMessage.NoPayConfiguration);

            RuleFor(x => x)
                .MustAsync(FeeCollectedIfCancelledAsync)
                .WithMessage(BusinessErrorMessage.NoCollectedFee);
        }

        private async Task<bool> FeeCollectedIfCancelledAsync(Command command, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetAll()
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order?.CancelledAt is null
                || await CollectedFeeAsync(
                    order, _receivableRepository, _refundRepository, _creditAccountRepository, cancellationToken) > 0m;
        }

        private async Task<bool> EmployeeIsAssignedToOrderAsync(Command command, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                        .GetAll()
                        .Include(o => o.AssignedEmployees)
                        .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            return order != null && order.AssignedEmployees.Any(oe => oe.EmployeeId == command.EmployeeId);
        }

        private Task<bool> ExistsWithOrderIdAndEmployeeIdAsync(Command command, CancellationToken cancellationToken) =>
            _orderEmployeePayRepository.ExistsWithOrderIdAndEmployeeIdAsync(command.OrderId, command.EmployeeId, cancellationToken);

        private async Task<bool> ConfigsExistAsync(Command command, CancellationToken cancellationToken)
        {
            var order = await _orderRepository
                .GetAll()
                .Include(o => o.SelectedServices)
                .Include(o => o.SelectedPackages)
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null)
            {
                return false;
            }

            // A seat paid from its frozen figures needs no rate today; the rate it was priced from may since
            // have been deleted.
            if (order.CancelledAt is not null
                || order.AssignedEmployees.Any(oe => oe.EmployeeId == command.EmployeeId && oe.JobBasePay is not null))
            {
                return true;
            }

            var serviceIds = order.SelectedServices.Select(os => os.ServiceId).ToList();
            var packageIds = order.SelectedPackages.Select(os => os.PackageId).ToList();

            // THE ORDER'S CURRENCY. A config denominated in anything else cannot pay this order, so
            // counting it here would pass the guard and leave the handler to derive a zero from an
            // empty set -- a cleaner assigned to a job that quotes nothing.
            return await _payConfigRepository.HasConfigForOrderAsync(
                serviceIds, packageIds, command.EmployeeId, [order.CurrencyId], cancellationToken);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IPayPeriodRepository payPeriodRepository,
        IEmployeePayConfigRepository payConfigRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        IReceivableRepository receivableRepository,
        IRefundRepository refundRepository,
        ICreditAccountRepository creditAccountRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetAll()
                .Include(o => o.SelectedServices)
                .Include(o => o.SelectedPackages)
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            // The validator proved the ORDER id exists with its own query; this one adds Includes and
            // could resolve differently. Guard rather than null-forgive.
            if (order is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            // Not a divergence but a genuine absence: there simply may be no open pay period, and
            // nothing upstream checks. Null-forgiving it made "payroll is between periods" a 500.
            var payPeriod = await payPeriodRepository.GetActivePeriodAsync(cancellationToken);
            if (payPeriod is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.NoActivePeriod));
            }

            var firstSeat = order.AssignedEmployees.MinBy(oe => oe.SeatOrdinal)?.EmployeeId == command.EmployeeId;

            if (order.CancelledAt is not null)
            {
                var collectedFee = await CollectedFeeAsync(
                    order, receivableRepository, refundRepository, creditAccountRepository, cancellationToken);
                var share = PayCalculatorExtensions.CalculateSeatFeeShare(
                    collectedFee, BookingPolicy.CleanerFeeShareRate, order.RequiredEmployees, firstSeat);
                var feeShare = OrderEmployeePay.CreateFeeShare(
                    orderId: command.OrderId,
                    employeeId: command.EmployeeId,
                    payPeriodId: payPeriod.Id,
                    currencyId: order.CurrencyId,
                    lineType: order.CancellationReason == OrderCancellationReasons.CustomerLockout
                        ? PayLineType.LockoutFeeShare
                        : PayLineType.CancellationFeeShare,
                    share: share,
                    payBreakdown: $"Collected fee: {collectedFee:F2}, Share: {share:F2}");

                orderEmployeePayRepository.Add(feeShare);

                return BusinessResult.Success(new Response(feeShare.Id));
            }

            // A seat whose contract for work formed is paid from the figures its reward was priced from, so a
            // rate edited after the take does not reach it. The seat's bounds are persisted with its pay so a
            // later bonus or deduction re-clamps the core exactly as it was clamped here.
            var seat = order.AssignedEmployees.FirstOrDefault(oe => oe.EmployeeId == command.EmployeeId);
            var (basePay, extrasPay, dirtinessPay, totalPay, minPay, maxPay, breakdown) =
                seat?.FrozenPay(order.DirtinessRate, order.RequiredEmployees, firstSeat)
                ?? (await LivePayConfigsAsync(order, command.EmployeeId, cancellationToken)).CalculateSeatPay(
                    order.Rooms,
                    order.Bathrooms,
                    order.DirtinessRate,
                    order.RequiredEmployees,
                    firstSeat);

            var orderEmployeePay = OrderEmployeePay.Create(
                orderId: command.OrderId,
                employeeId: command.EmployeeId,
                payPeriodId: payPeriod!.Id,
                // The order's currency, which is also the pay configs' -- the rates are read, at the take
                // or here, only in it, so the amounts below were computed in this currency rather than
                // merely labelled with it.
                currencyId: order.CurrencyId,
                basePay: basePay,
                extrasPay: extrasPay,
                dirtinessPay: dirtinessPay,
                totalPay: totalPay,
                minPay: minPay,
                maxPay: maxPay,
                payBreakdown: breakdown);

            orderEmployeePayRepository.Add(orderEmployeePay);

            order.MarkEmployeePayCalculated();

            return BusinessResult.Success(new Response(orderEmployeePay.Id));
        }

        // Scoped to the order's own currency, so SelectPreferredConfigs below is choosing between an
        // override and a platform-wide row rather than between denominations.
        private async Task<List<EmployeePayConfig>> LivePayConfigsAsync(
            Order order, string employeeId, CancellationToken cancellationToken)
        {
            var serviceIds = order.SelectedServices.Select(os => os.ServiceId).ToList();
            var packageIds = order.SelectedPackages.Select(os => os.PackageId).ToList();

            var serviceConfigs = await payConfigRepository.GetServiceConfigsForOrderAsync(
                serviceIds, employeeId, [order.CurrencyId], cancellationToken);
            var packageConfigs = await payConfigRepository.GetPackageConfigsForOrderAsync(
                packageIds, employeeId, [order.CurrencyId], cancellationToken);

            return
            [
                .. SelectPreferredConfigs(packageConfigs, c => c.PackageId),
                .. SelectPreferredConfigs(serviceConfigs, c => c.ServiceId),
            ];
        }

        private static IEnumerable<EmployeePayConfig> SelectPreferredConfigs(
            IEnumerable<EmployeePayConfig> configs,
            Func<EmployeePayConfig, string?> targetIdSelector)
        {
            return configs
                .GroupBy(targetIdSelector)
                .Where(g => g.Key != null)
                .Select(g => g.FirstOrDefault(c => c.EmployeeId != null) ?? g.First());
        }
    }
}
