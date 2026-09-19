using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.AppServices.Services.Interfaces;
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
        private readonly ICurrencyResolutionService _currencyResolutionService;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IEmployeeInvoiceRepository invoiceRepository,
            IEmployeePayoutDetailsRepository payoutDetailsRepository,
            ICurrencyResolutionService currencyResolutionService) : base(userRepository, userSessionProvider)
        {
            _invoiceRepository = invoiceRepository;
            _payoutDetailsRepository = payoutDetailsRepository;
            _currencyResolutionService = currencyResolutionService;
            RuleFor(x => x.InvoiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(invoiceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.InvoiceNotFound)
                .MustAsync(BePendingStatusAsync)
                .WithMessage(BusinessErrorMessage.InvalidInvoiceStatus)
                .MustAsync(PayoutDetailsAreUsableAsync)
                .WithMessage(BusinessErrorMessage.InvoicePayoutDetailsMissing)
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
        // bank, outside the platform -- so this is the last point where the platform can still say no.
        // MarkInvoicePaid is too late (the money has left) and generation is too early (it would
        // withhold a sequence-numbered tax document). That reasoning put the currency rule here first,
        // and it is why ADR-0034 D7's presence gate sits here too (correction of 2026-09-12) rather than
        // at generation as the ADR was written: the document is always issued on time, only the
        // payment waits until the cleaner has somewhere usable to receive it. Presence is judged before
        // currency, so an absent record is never reported as a currency mismatch.
        private async Task<bool> PayoutDetailsAreUsableAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            var payout = await _payoutDetailsRepository.GetByEmployeeIdAsync(invoice!.EmployeeId, cancellationToken);
            return payout is { Scheme: not null, Status: PayoutDetailsStatus.Provided };
        }

        // A cleaner is paid in the currency of the country they work in (owner ruling 2026-09-12: CZ is
        // CZK, SK is EUR, PL is PLN -- never the platform default), so an account that has not declared
        // a currency is taken to hold that one, resolved through the same work-country chain every
        // partner screen uses. The declaration is the exception: an account that holds something other
        // than its market's currency. The record is present here: the presence rule ran first.
        private async Task<bool> PayoutAccountHoldsInvoiceCurrencyAsync(string invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
            var payout = await _payoutDetailsRepository.GetByEmployeeIdAsync(invoice!.EmployeeId, cancellationToken);

            var accountCurrencyId = payout!.CurrencyId
                ?? (await _currencyResolutionService.ResolveCurrencyForEmployeeAsync(invoice.EmployeeId, cancellationToken)).Id;

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
