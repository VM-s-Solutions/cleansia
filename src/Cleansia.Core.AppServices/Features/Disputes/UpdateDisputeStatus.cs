using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class UpdateDisputeStatus
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DisputeId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.NewStatus)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);
        }
    }

    public record Command(
        string DisputeId,
        DisputeStatus NewStatus
    ) : ICommand;

    public class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var dispute = await disputeRepository.GetByIdAsync(request.DisputeId, cancellationToken);

            if (dispute == null)
            {
                return BusinessResult.Failure(new Error(nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            dispute.UpdateStatus(request.NewStatus, actorId);

            return BusinessResult.Success();
        }
    }
}
