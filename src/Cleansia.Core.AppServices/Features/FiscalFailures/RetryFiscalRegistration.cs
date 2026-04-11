using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.FiscalFailures;

/// <summary>
/// Admin command to manually schedule an immediate fiscal-retry attempt for a specific receipt.
/// The actual retry runs on the next tick of the <c>RetryFailedFiscalRegistrations</c> timer
/// (every 5 minutes), so the admin sees the state change eventually without blocking the request.
/// </summary>
public class RetryFiscalRegistration
{
    public record Command(string ReceiptId) : ICommand;

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderReceiptRepository receiptRepository)
        {
            RuleFor(x => x.ReceiptId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(receiptRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ReceiptNotFound);
        }
    }

    public class Handler(IOrderReceiptRepository receiptRepository)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            var receipt = await receiptRepository.GetByIdAsync(command.ReceiptId, cancellationToken);
            receipt!.ScheduleImmediateFiscalRetry();
            return BusinessResult.Success();
        }
    }
}
