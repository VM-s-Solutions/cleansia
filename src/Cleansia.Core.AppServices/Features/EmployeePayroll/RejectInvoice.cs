using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class RejectInvoice
{
    public record Command(
        string InvoiceId,
        string AdminNotes) : ICommand<Response>;

    public record Response(string InvoiceId);

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
                .MustAsync(NotPaidAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyPaid);

            RuleFor(x => x.AdminNotes)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> NotPaidAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return invoice!.Status != EmployeeInvoiceStatus.Paid;
        }
    }

    public class Handler(IEmployeeInvoiceRepository invoiceRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);

            if (invoice!.Status == EmployeeInvoiceStatus.Paid)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.InvoiceId), BusinessErrorMessage.InvoiceAlreadyPaid));
            }

            invoice.Reject(command.AdminNotes);

            return BusinessResult.Success(new Response(invoice.Id));
        }
    }
}
