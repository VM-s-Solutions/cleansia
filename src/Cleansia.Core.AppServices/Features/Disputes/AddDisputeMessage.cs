using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class AddDisputeMessage
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(IDisputeRepository disputeRepository)
        {
            RuleFor(x => x.DisputeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(disputeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.DisputeNotFound);

            RuleFor(x => x.Message)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);
        }
    }

    public record Command(
        string DisputeId,
        string Message,
        bool IsStaffMessage
    ) : ICommand;

    public class Handler(IDisputeRepository disputeRepository) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var dispute = await disputeRepository.GetDisputeWithDetailsAsync(request.DisputeId);

            // For now, use the dispute's user ID as the author
            var authorId = dispute.UserId;

            dispute.AddMessage(
                message: request.Message,
                authorId: authorId,
                isStaff: request.IsStaffMessage
            );

            return BusinessResult.Success();
        }
    }
}
