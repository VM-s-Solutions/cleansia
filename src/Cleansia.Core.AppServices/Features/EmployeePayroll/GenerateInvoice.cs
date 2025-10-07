using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class GenerateInvoice
{
    public record Command(
        string EmployeeId,
        string PayPeriodId) : ICommand<Response>;

    public record Response(string InvoiceId);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeInvoiceRepository _employeeInvoiceRepository;
        private readonly IOrderEmployeePayRepository _orderEmployeePayRepository;

        public Validator(
            IEmployeeRepository employeeRepository,
            IPayPeriodRepository payPeriodRepository,
            IEmployeeInvoiceRepository employeeInvoiceRepository,
            IOrderEmployeePayRepository orderEmployeePayRepository)
        {
            _employeeInvoiceRepository = employeeInvoiceRepository;
            _orderEmployeePayRepository = orderEmployeePayRepository;

            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.PayPeriodId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payPeriodRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotFound);

            RuleFor(x => x)
                .MustAsync(ExistsForPayPeriodAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyExists);

            RuleFor(x => x)
                .MustAsync(NoUnpaidOrderPaysExist)
                .WithMessage(BusinessErrorMessage.NoUnpaidOrderPays);
        }

        private Task<bool> ExistsForPayPeriodAsync(Command command, CancellationToken cancellationToken) =>
            _employeeInvoiceRepository.ExistsForPayPeriodAsync(command.EmployeeId, command.PayPeriodId,
                cancellationToken);

        private Task<bool> NoUnpaidOrderPaysExist(Command command, CancellationToken cancellationToken) =>
            _orderEmployeePayRepository.GetByEmployeeId(command.EmployeeId)
                .Where(p => p.PayPeriodId == command.PayPeriodId && p.EmployeeInvoiceId == null)
                .AnyAsync(cancellationToken);
    }

    public class Handler(
        ICurrencyRepository currencyRepository,
        IEmployeeRepository employeeRepository,
        IEmployeeInvoiceRepository invoiceRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var orderPays = await orderEmployeePayRepository
                .GetByEmployeeId(command.EmployeeId)
                .Where(p => p.PayPeriodId == command.PayPeriodId && p.EmployeeInvoiceId == null)
                .ToListAsync(cancellationToken);

            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            var subTotal = orderPays.Sum(p => p.BasePay + p.ExtrasPay + p.ExpensesPay);
            var bonusAmount = orderPays.Sum(p => p.BonusPay);
            var deductionAmount = orderPays.Sum(p => p.DeductionPay);

            var currency = await currencyRepository.GetByCodeAsync(employee!.PreferredCurrencyCode ?? string.Empty, cancellationToken) ??
                           await currencyRepository.GetDefaultAsync(cancellationToken);

            var invoice = EmployeeInvoice.Create(
                command.EmployeeId,
                command.PayPeriodId,
                orderPays.Count,
                subTotal,
                currency.Id,
                bonusAmount,
                deductionAmount);

            invoiceRepository.Add(invoice);

            foreach (var orderPay in orderPays)
            {
                orderPay.AssignToInvoice(invoice.Id);
            }

            return BusinessResult.Success(new Response(invoice.Id));
        }
    }
}
