using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayPeriods;

public class ClosePayPeriod
{
    public record Command(
        string PayPeriodId,
        string? Notes) : ICommand<Response>;

    public record Response(string PayPeriodId);

    public class Validator : UserEmailValidator<Command>
    {
        private readonly IPayPeriodRepository _payPeriodRepository;
        private readonly IEmployeeInvoiceRepository _invoiceRepository;

        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider,
            IPayPeriodRepository payPeriodRepository,
            IEmployeeInvoiceRepository invoiceRepository) : base(userRepository, userSessionProvider)
        {
            _payPeriodRepository = payPeriodRepository;
            _invoiceRepository = invoiceRepository;

            RuleFor(x => x.PayPeriodId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payPeriodRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotFound)
                .MustAsync(BeOpenStatusAsync)
                .WithMessage(BusinessErrorMessage.PayPeriodNotOpen)
                .MustAsync(BeNoUnpaidInvoicesAsync)
                .WithMessage(BusinessErrorMessage.UnpaidInvoicesExist);

            RuleFor(x => x.Notes)
                .MaximumLength(1000)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }

        private Task<bool> BeOpenStatusAsync(string payPeriodId, CancellationToken cancellationToken) =>
            _payPeriodRepository.GetByIdAsync(payPeriodId, cancellationToken)
                .ContinueWith(t => t.Result!.Status == PayPeriodStatus.Open, cancellationToken);

        private Task<bool> BeNoUnpaidInvoicesAsync(string payPeriodId, CancellationToken cancellationToken) =>
            _invoiceRepository.GetByPayPeriodId(payPeriodId)
                .Where(i => i.Status != EmployeeInvoiceStatus.Paid)
                .CountAsync(cancellationToken)
                .ContinueWith(t => t.Result == 0, cancellationToken);
    }

    public class Handler(
        IPayPeriodRepository payPeriodRepository,
        IUserSessionProvider userSessionProvider)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var payPeriod = await payPeriodRepository.GetByIdAsync(command.PayPeriodId, cancellationToken);
            var adminEmail = userSessionProvider.GetUserEmail();
            payPeriod!.Close(adminEmail!, command.Notes);

            return BusinessResult.Success(new Response(payPeriod.Id));
        }
    }
}
