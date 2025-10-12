using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.EmployeePayroll;

public class ApproveInvoice
{
    public record Command(
        string InvoiceId,
        string? AdminNotes) : ICommand<Response>;

    public record Response(string InvoiceId);

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IEmployeeInvoiceRepository _invoiceRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeeInvoiceRepository invoiceRepository) : base(userRepository, userSessionProvider)
        {
            _invoiceRepository = invoiceRepository;
            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound)
                .MustAsync(BePendingStatusAsync)
                .WithMessage(BusinessErrorMessage.InvalidInvoiceStatus);

            RuleFor(x => x.AdminNotes)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private Task<bool> BePendingStatusAsync(string invoiceId, CancellationToken cancellationToken) =>
            _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken)
                .ContinueWith(t => t.Result!.Status == EmployeeInvoiceStatus.Pending, cancellationToken);
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IEmployeeInvoiceRepository invoiceRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId, cancellationToken);
            var adminEmail = userSessionProvider.GetUserEmail();
            invoice!.Approve(adminEmail!, command.AdminNotes);

            return BusinessResult.Success(new Response(invoice.Id));
        }
    }
}
