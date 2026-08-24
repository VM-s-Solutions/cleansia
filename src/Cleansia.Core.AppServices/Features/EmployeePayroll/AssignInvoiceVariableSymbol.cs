using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

/// <summary>
/// The one-time assignment for an invoice created before payout references existed (ADR-0046 §D4.3).
/// There is no migration and no sweep: a symbol written onto a row whose STORED PDF does not print it
/// renders as authoritative on three surfaces and appears on no document, which is strictly worse than
/// NULL because NULL is honestly empty. So the assignment and the re-render travel together.
/// </summary>
public class AssignInvoiceVariableSymbol
{
    public record Command(string InvoiceId, string LanguageCode) : ICommand<Response>;

    public record Response(string InvoiceId, string VariableSymbol, string? PdfBlobUrl);

    private static readonly EmployeeInvoiceStatus[] AssignableStatuses =
    [
        EmployeeInvoiceStatus.Pending,
        EmployeeInvoiceStatus.Approved,
        EmployeeInvoiceStatus.Disputed
    ];

    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeInvoiceRepository _invoiceRepository;

        public Validator(
            IEmployeeInvoiceRepository invoiceRepository,
            ILanguageRepository languageRepository)
        {
            _invoiceRepository = invoiceRepository;

            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound)
                .MustAsync(NotCancelledAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyCancelled)
                .MustAsync(NotPaidAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyPaid)
                .MustAsync(AssignableStatusAsync)
                .WithMessage(BusinessErrorMessage.InvalidInvoiceStatus)
                .MustAsync(HasNoVariableSymbolAsync)
                .WithMessage(BusinessErrorMessage.InvoiceReferenceAlreadyAssigned);

            RuleFor(x => x.LanguageCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(languageRepository.ExistsWithCodeAsync)
                .WithMessage(BusinessErrorMessage.LanguageNotFound);
        }

        private async Task<bool> NotCancelledAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return !invoice!.IsCancelled;
        }

        private async Task<bool> NotPaidAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return invoice!.Status != EmployeeInvoiceStatus.Paid;
        }

        private async Task<bool> AssignableStatusAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return AssignableStatuses.Contains(invoice!.Status);
        }

        private async Task<bool> HasNoVariableSymbolAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return string.IsNullOrEmpty(invoice!.VariableSymbol);
        }
    }

    public class Handler(
        IEmployeeInvoiceRepository invoiceRepository,
        IPayoutReferenceAllocator payoutReferenceAllocator,
        IMediator mediator,
        ILogger<Handler> logger)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);
            if (invoice is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.InvoiceId), BusinessErrorMessage.InvoiceNotFound));
            }

            var variableSymbol = await payoutReferenceAllocator.AllocateAsync(cancellationToken);
            if (variableSymbol.IsFailure)
            {
                return BusinessResult.Failure<Response>(variableSymbol.Error!);
            }

            invoice.AssignVariableSymbol(variableSymbol.Value!);

            // Stamp and COMMIT before re-rendering. The order is load-bearing: if the render fails the
            // row keeps its number and the regenerate is re-runnable and idempotent (it re-renders the
            // SAME stored symbol over the same blob name). The reverse order would put a number on a
            // document and not on the row.
            try
            {
                await invoiceRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                invoiceRepository.Rollback();

                return BusinessResult.Failure<Response>(new Error(
                    nameof(EmployeeInvoice.VariableSymbol),
                    BusinessErrorMessage.InvoiceReferenceUnavailable));
            }

            var regenerated = await mediator.Send(
                new RegenerateInvoicePdf.Command(command.InvoiceId, command.LanguageCode), cancellationToken);

            if (regenerated.IsFailure)
            {
                // The reference is durable; only the document is stale. Surfacing this as a failure
                // would invite a retry of the whole command, which now refuses (the row has a symbol).
                logger.LogError(
                    "Assigned variable symbol {VariableSymbol} to invoice {InvoiceId} but the PDF regenerate failed ({Error}); re-run the regenerate",
                    variableSymbol.Value, command.InvoiceId, regenerated.Error?.Message);
            }

            return BusinessResult.Success(new Response(
                command.InvoiceId, variableSymbol.Value!, regenerated.Value?.PdfBlobUrl));
        }
    }
}
