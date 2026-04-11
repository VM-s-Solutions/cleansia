using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.FiscalFailures;

/// <summary>
/// Admin command to mark a fiscal failure as acknowledged. Acknowledged failures are
/// hidden from the default admin dashboard view and will not be retried automatically.
/// </summary>
public class AcknowledgeFiscalFailure
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
            receipt!.AcknowledgeFiscalFailure();
            return BusinessResult.Success();
        }
    }
}
