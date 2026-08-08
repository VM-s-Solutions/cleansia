using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class MarkInvoicePaid
{
    public record Command(
        string InvoiceId,
        string? BankTransferNote,
        string? AdminNotes) : ICommand<Response>;

    public record Response(string InvoiceId);

    // Paid is terminal — Dispute, Reject, UpdateAmounts and Cancel all refuse a paid invoice — so the
    // mark cannot be undone and a second one must not overwrite the first actor's record of a transfer
    // that already left the bank.
    private static string? RefusalFor(EmployeeInvoiceStatus status) => status switch
    {
        EmployeeInvoiceStatus.Approved => null,
        EmployeeInvoiceStatus.Paid => BusinessErrorMessage.InvoiceAlreadyPaid,
        EmployeeInvoiceStatus.Cancelled => BusinessErrorMessage.InvoiceAlreadyCancelled,
        _ => BusinessErrorMessage.InvoiceNotApproved
    };

    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeInvoiceRepository _invoiceRepository;

        public Validator(IEmployeeInvoiceRepository invoiceRepository)
        {
            _invoiceRepository = invoiceRepository;

            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound)
                .MustAsync(NotAlreadyPaidAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyPaid)
                .MustAsync(NotCancelledAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyCancelled)
                .MustAsync(ApprovedAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotApproved);

            RuleFor(x => x.BankTransferNote)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.AdminNotes)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> NotAlreadyPaidAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return invoice!.Status != EmployeeInvoiceStatus.Paid;
        }

        private async Task<bool> NotCancelledAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return invoice!.Status != EmployeeInvoiceStatus.Cancelled;
        }

        private async Task<bool> ApprovedAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return invoice!.Status == EmployeeInvoiceStatus.Approved;
        }
    }

    public class Handler(
        IEmployeeInvoiceRepository invoiceRepository,
        INotificationProducer notificationProducer)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);

            // The validator's status read and this write are separate steps: a second admin marking the
            // same invoice in between reaches MarkAsPaid's throw, which would surface as a 500 rather
            // than a refusal and hide a possible double transfer.
            var refusal = RefusalFor(invoice!.Status);
            if (refusal is not null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.InvoiceId), refusal));
            }

            invoice.MarkAsPaid(command.BankTransferNote, command.AdminNotes);

            // "You've been paid" — the cleaner's highest-value payroll signal (GetByIdAsync already
            // loads Employee.User). Skips a legacy invoice with no linked user. Feed row + push ride
            // this command's unit of work via the producer seam.
            var employeeUserId = invoice.Employee?.UserId;
            if (!string.IsNullOrEmpty(employeeUserId))
            {
                await notificationProducer.NotifyAsync(
                    employeeUserId,
                    NotificationEventCatalog.InvoicePaid,
                    new Dictionary<string, string> { ["invoiceId"] = invoice.Id },
                    invoice.TenantId,
                    invoice.Id,
                    cancellationToken);
            }

            return BusinessResult.Success(new Response(command.InvoiceId));
        }
    }
}
