using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class CancelInvoice
{
    public record Command(
        string InvoiceId,
        string Reason) : ICommand<Response>;

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
                .WithMessage(BusinessErrorMessage.InvoiceNotFound);

            RuleFor(x => x.Reason)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(x => x)
                .MustAsync(InvoiceNotPaidAsync)
                .WithMessage(BusinessErrorMessage.CannotCancelPaidInvoice);

            RuleFor(x => x)
                .MustAsync(InvoiceNotAlreadyCancelledAsync)
                .WithMessage(BusinessErrorMessage.InvoiceAlreadyCancelled);
        }

        private async Task<bool> InvoiceNotPaidAsync(Command command, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);
            return invoice?.Status != EmployeeInvoiceStatus.Paid;
        }

        private async Task<bool> InvoiceNotAlreadyCancelledAsync(Command command, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);
            return invoice?.IsCancelled != true;
        }
    }

    public class Handler(
        IEmployeeInvoiceRepository invoiceRepository,
        IUserSessionProvider userSessionProvider)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);

            if (invoice == null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.InvoiceId),
                    BusinessErrorMessage.InvoiceNotFound));
            }

            try
            {
                invoice.Cancel(command.Reason, actorId);
            }
            catch (InvalidOperationException ex)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.InvoiceId),
                    ex.Message));
            }

            return BusinessResult.Success(new Response(invoice.Id));
        }
    }
}
