using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class ResolveDispute
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DisputeId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.ResolutionNotes)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(x => x.RefundAmount)
                .GreaterThanOrEqualTo(0)
                .When(x => x.RefundAmount.HasValue)
                .WithMessage(BusinessErrorMessage.InvalidRefundAmount);
        }
    }

    public record Command(
        string DisputeId,
        decimal? RefundAmount,
        string ResolutionNotes
    ) : ICommand;

    public class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var dispute = await disputeRepository.GetDisputeWithDetailsAsync(request.DisputeId);

            if (dispute == null)
            {
                return BusinessResult.Failure(new Error(nameof(request.DisputeId), BusinessErrorMessage.DisputeNotFound));
            }

            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            dispute.Resolve(
                resolvedBy: actorId,
                refundAmount: request.RefundAmount,
                resolutionNotes: request.ResolutionNotes
            );

            return BusinessResult.Success();
        }
    }
}
