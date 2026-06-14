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
    ) : ICommand<Response>;

    public record Response(string DisputeId, DisputeStatus Status);

    public class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var dispute = await disputeRepository.GetForUpdateAsync(request.DisputeId, cancellationToken);

            if (dispute == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;

            if (!dispute.UpdateStatus(request.NewStatus, actorId))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(request.NewStatus), BusinessErrorMessage.InvalidDisputeStatusTransition));
            }

            return BusinessResult.Success(new Response(dispute.Id, dispute.Status));
        }
    }
}
