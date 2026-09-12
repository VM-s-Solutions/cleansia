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
        private readonly IEmployeePayoutDetailsRepository _payoutDetailsRepository;
        private readonly ICurrencyRepository _currencyRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeeInvoiceRepository invoiceRepository,
            IEmployeePayoutDetailsRepository payoutDetailsRepository,
            ICurrencyRepository currencyRepository) : base(userRepository, userSessionProvider)
        {
            _invoiceRepository = invoiceRepository;
            _payoutDetailsRepository = payoutDetailsRepository;
            _currencyRepository = currencyRepository;
            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound)
                .MustAsync(BePendingStatusAsync)
                .WithMessage(BusinessErrorMessage.InvalidInvoiceStatus)
                .MustAsync(PayoutAccountHoldsInvoiceCurrencyAsync)
                .WithMessage(BusinessErrorMessage.InvoicePayoutCurrencyMismatch);

            RuleFor(x => x.AdminNotes)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private async Task<bool> BePendingStatusAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            return invoice!.Status == EmployeeInvoiceStatus.Pending;
        }

        // Approval is the admin's commitment to transfer, and the transfer itself is keyed by hand in a
        // bank, outside the platform -- so this is the last point where the platform can still say no
        // (T-0708). MarkInvoicePaid is too late (the money has left) and generation is too early (it
        // would withhold a sequence-numbered tax document). An account that has not declared a currency
        // is taken to hold the platform default, which is the assumption every destination was collected
        // under. A MISSING record is not this rule's concern: that is ADR-0034 D7's presence gate, and it
        // is not built here.
        private async Task<bool> PayoutAccountHoldsInvoiceCurrencyAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            var payout = await _payoutDetailsRepository.GetByEmployeeIdAsync(invoice!.EmployeeId, cancellationToken);
            if (payout is null)
            {
                return true;
            }

            var accountCurrencyId = payout.CurrencyId
                ?? (await _currencyRepository.GetDefaultAsync(cancellationToken)).Id;

            return accountCurrencyId == invoice.CurrencyId;
        }
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IEmployeeInvoiceRepository invoiceRepository)
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

            var adminEmail = userSessionProvider.GetUserEmail();
            invoice.Approve(adminEmail!, command.AdminNotes);

            return BusinessResult.Success(new Response(invoice.Id));
        }
    }
}
