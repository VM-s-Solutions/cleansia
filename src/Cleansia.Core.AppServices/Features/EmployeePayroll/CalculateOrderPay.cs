using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class CalculateOrderPay
{
    public record Command(string OrderId, string EmployeeId) : ICommand<Response>;

    public record Response(string EmployeePayrollId);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IEmployeePayConfigRepository _payConfigRepository;
        private readonly IOrderEmployeePayRepository _orderEmployeePayRepository;

        public Validator(
            IOrderRepository orderRepository,
            IEmployeeRepository employeeRepository,
            IPayPeriodRepository payPeriodRepository,
            IEmployeePayConfigRepository payConfigRepository,
            IOrderEmployeePayRepository orderEmployeePayRepository)
        {
            _orderRepository = orderRepository;
            _payConfigRepository = payConfigRepository;
            _orderEmployeePayRepository = orderEmployeePayRepository;

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

            RuleFor(x => x)
                .MustAsync(ExistsWithOrderIdAndEmployeeIdAsync)
                .WithMessage(BusinessErrorMessage.PayAlreadyCalculated);

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync((_, ct) => payPeriodRepository.ExistsActivePeriodAsync(ct))
                .WithMessage(BusinessErrorMessage.NoActivePeriod);

            RuleFor(x => x)
                .MustAsync(ConfigsExistAsync)
                .WithMessage(BusinessErrorMessage.NoPayConfiguration);
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
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order is null)
            {
                return false;
            }

            var serviceIds = order.SelectedServices.Select(os => os.ServiceId).ToList();
            var packageIds = order.SelectedPackages.Select(os => os.PackageId).ToList();

            return await _payConfigRepository.HasConfigForOrderAsync(
                serviceIds, packageIds, command.EmployeeId, cancellationToken);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IPayPeriodRepository payPeriodRepository,
        IEmployeePayConfigRepository payConfigRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetAll()
                .Include(o => o.SelectedServices)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            var payPeriod = await payPeriodRepository.GetActivePeriodAsync(cancellationToken);

            var serviceIds = order!.SelectedServices.Select(os => os.ServiceId).ToList();
            var packageIds = order!.SelectedPackages.Select(os => os.PackageId).ToList();

            var payConfigs = new List<EmployeePayConfig>();

            var serviceConfigs = await payConfigRepository.GetServiceConfigsForOrderAsync(
                serviceIds, command.EmployeeId, cancellationToken);
            var packageConfigs = await payConfigRepository.GetPackageConfigsForOrderAsync(
                packageIds, command.EmployeeId, cancellationToken);

            payConfigs.AddRange(SelectPreferredConfigs(packageConfigs, c => c.PackageId));
            payConfigs.AddRange(SelectPreferredConfigs(serviceConfigs, c => c.ServiceId));

            var (basePay, extrasPay, expensesPay, totalPay, breakdown) = payConfigs.CalculateAggregatedPay(order);

            var orderEmployeePay = OrderEmployeePay.Create(
                orderId: command.OrderId,
                employeeId: command.EmployeeId,
                payPeriodId: payPeriod!.Id,
                basePay: basePay,
                extrasPay: extrasPay,
                expensesPay: expensesPay,
                totalPay: totalPay,
                payBreakdown: breakdown);

            orderEmployeePayRepository.Add(orderEmployeePay);

            order.MarkEmployeePayCalculated();

            return BusinessResult.Success(new Response(orderEmployeePay.Id));
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
