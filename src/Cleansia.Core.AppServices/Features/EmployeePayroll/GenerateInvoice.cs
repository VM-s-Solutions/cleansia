using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
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
                .MustAsync(NoInvoiceExistsForPayPeriodAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyExists);

            RuleFor(x => x)
                .MustAsync(NoUnpaidOrderPaysExist)
                .WithMessage(BusinessErrorMessage.NoUnpaidOrderPays);
        }

        // The rule must PASS when there is NO existing invoice (so a first-time generation
        // proceeds) and FAIL with "already exists" when one is already present (the at-least-once
        // redelivery dedup). MustAsync passes on a true predicate, so the existence check is negated.
        private async Task<bool> NoInvoiceExistsForPayPeriodAsync(Command command, CancellationToken cancellationToken) =>
            !await _employeeInvoiceRepository.ExistsForPayPeriodAsync(command.EmployeeId, command.PayPeriodId,
                cancellationToken);

        private Task<bool> NoUnpaidOrderPaysExist(Command command, CancellationToken cancellationToken) =>
            _orderEmployeePayRepository.HasUnassignedForEmployeePeriodAsync(
                command.EmployeeId, command.PayPeriodId, cancellationToken);
    }

    public class Handler(
        ICurrencyRepository currencyRepository,
        ICurrencyResolutionService currencyResolutionService,
        IEmployeeInvoiceRepository invoiceRepository,
        IOrderEmployeePayRepository orderEmployeePayRepository,
        IPayoutReferenceAllocator payoutReferenceAllocator)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var orderPays = await orderEmployeePayRepository.GetUnassignedForEmployeePeriodAsync(
                command.EmployeeId, command.PayPeriodId, cancellationToken);

            var currencyCode = await currencyResolutionService
                .ResolveCurrencyCodeForEmployeeAsync(command.EmployeeId, cancellationToken);
            var currency = (currencyCode is not null
                ? await currencyRepository.GetByCodeAsync(currencyCode, cancellationToken)
                : null) ?? await currencyRepository.GetDefaultAsync(cancellationToken);

            var variableSymbol = await payoutReferenceAllocator.AllocateAsync(cancellationToken);
            if (variableSymbol.IsFailure)
            {
                return BusinessResult.Failure<Response>(variableSymbol.Error!);
            }

            var invoice = EmployeeInvoice.CreateFromOrderPays(
                command.EmployeeId,
                command.PayPeriodId,
                orderPays,
                currency.Id,
                variableSymbol.Value!);

            invoiceRepository.Add(invoice);

            foreach (var orderPay in orderPays)
            {
                orderPay.AssignToInvoice(invoice.Id);
            }

            // This handler does NOT own its own commit — UnitOfWorkPipelineBehavior commits after it
            // returns, so a duplicate variable symbol would reach the unique index there and surface as
            // an unhandled DbUpdateException (a 500 on the admin path, a poisoned message on the queue
            // one). FLUSH here and own the failure: the allocator makes a duplicate impossible by
            // construction, so this is the backstop for a hand-written INSERT or a restored counter row,
            // and it must be a refusal the admin can act on rather than a stack trace.
            try
            {
                await invoiceRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                // Detach the rejected insert so the pipeline's later CommitAsync cannot retry it and
                // re-raise the same 23505. Nothing was persisted, so Remove() on a still-Added entity
                // simply detaches it.
                invoiceRepository.Remove(invoice);

                return BusinessResult.Failure<Response>(new Error(
                    nameof(EmployeeInvoice.VariableSymbol),
                    BusinessErrorMessage.InvoiceReferenceUnavailable));
            }

            return BusinessResult.Success(new Response(invoice.Id));
        }
    }
}
