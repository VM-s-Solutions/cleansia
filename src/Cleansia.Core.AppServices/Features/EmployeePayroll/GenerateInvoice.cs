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

    /// <summary>One id per invoice written -- one per currency the period's unassigned pay holds.</summary>
    public record Response(IReadOnlyList<string> InvoiceIds);

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
                .MustAsync(NoInvoiceExistsForAnUnassignedPayCurrencyAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyExists);

            RuleFor(x => x)
                .MustAsync(NoUnpaidOrderPaysExist)
                .WithMessage(BusinessErrorMessage.NoUnpaidOrderPays);
        }

        // PER CURRENCY: one invoice per (employee, period, currency), so the rule passes while every
        // not-yet-invoiced row is in a currency the pair has no invoice for, and fails "already exists"
        // when a row would join an invoice that already exists (a late pay row). At-least-once
        // redelivery is NOT dedup'd here any more -- after a full run there is no unassigned row left,
        // so the NoUnpaidOrderPays rule below is what makes a second delivery a no-op. MustAsync passes
        // on a true predicate, so the existence check is negated.
        private async Task<bool> NoInvoiceExistsForAnUnassignedPayCurrencyAsync(Command command, CancellationToken cancellationToken) =>
            !await _employeeInvoiceRepository.ExistsForUnassignedPayCurrencyAsync(command.EmployeeId, command.PayPeriodId,
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

            // NOTHING TO INVOICE. Reachable here in a way it is not in the background sweep, which
            // skips an employee with no unassigned pay -- this endpoint would otherwise write a
            // zero-value tax document with no currency to derive.
            if (orderPays.Count == 0)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.EmployeeId), BusinessErrorMessage.NoUnpaidOrderPays));
            }

            // THE CURRENCY COMES FROM THE ROWS BEING INVOICED. It used to come from the work country's
            // default currency via ICurrencyResolutionService, while the background sweep derived it
            // from Employee.PreferredCurrencyCode -- two answers for one document, neither of which read
            // the pay rows. Both are deleted.
            //
            // ONE INVOICE PER CURRENCY. A cleaner can work an order in each, and a tax document is in
            // one unit, so the honest representation of a mixed period is two documents -- not a
            // refusal, which had no admin action to resolve it (a EUR job cannot be made a CZK job) and
            // stranded the rows with no invoice forever while the sweep re-enqueued the pair every tick.
            var groups = orderPays
                .GroupBy(p => p.CurrencyId)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToList();

            // Every payout reference is claimed BEFORE any invoice is staged, so a refused allocation
            // leaves nothing half-written.
            var symbols = new List<string>(groups.Count);
            foreach (var _ in groups)
            {
                var variableSymbol = await payoutReferenceAllocator.AllocateAsync(cancellationToken);
                if (variableSymbol.IsFailure)
                {
                    return BusinessResult.Failure<Response>(variableSymbol.Error!);
                }
                symbols.Add(variableSymbol.Value!);
            }

            var invoices = new List<EmployeeInvoice>(groups.Count);
            for (var i = 0; i < groups.Count; i++)
            {
                var rows = groups[i].ToList();
                var invoice = EmployeeInvoice.CreateFromOrderPays(
                    command.EmployeeId,
                    command.PayPeriodId,
                    rows,
                    symbols[i]);

                invoiceRepository.Add(invoice);
                foreach (var orderPay in rows)
                {
                    orderPay.AssignToInvoice(invoice.Id);
                }
                invoices.Add(invoice);
            }

            // This handler does NOT own its own commit — UnitOfWorkPipelineBehavior commits after it
            // returns, so a duplicate variable symbol would reach the unique index there and surface as
            // an unhandled DbUpdateException (a 500 on the admin path, a poisoned message on the queue
            // one). FLUSH here and own the failure: the allocator makes a duplicate impossible by
            // construction, so this is the backstop for a hand-written INSERT or a restored counter row,
            // and it must be a refusal the admin can act on rather than a stack trace. One flush for
            // every currency's invoice, so a period is invoiced whole or not at all.
            try
            {
                await invoiceRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                // Detach the rejected inserts so the pipeline's later CommitAsync cannot retry them and
                // re-raise the same 23505. Nothing was persisted, so Remove() on a still-Added entity
                // simply detaches it.
                foreach (var invoice in invoices)
                {
                    invoiceRepository.Remove(invoice);
                }

                return BusinessResult.Failure<Response>(new Error(
                    nameof(EmployeeInvoice.VariableSymbol),
                    BusinessErrorMessage.InvoiceReferenceUnavailable));
            }

            return BusinessResult.Success(new Response(invoices.Select(i => i.Id).ToList()));
        }
    }
}
