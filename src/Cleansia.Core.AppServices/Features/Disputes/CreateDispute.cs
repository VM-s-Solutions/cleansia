using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Disputes;

public class CreateDispute
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.Reason)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);

            RuleFor(x => x.Description)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MinimumLength(10)
                .WithMessage(BusinessErrorMessage.MinLength)
                .MaximumLength(2000)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);
        }
    }

    public record Command(
        string OrderId,
        DisputeReason Reason,
        string Description
    ) : ICommand<string>;

    public class Handler(
        IDisputeRepository disputeRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, string>
    {
        public async Task<BusinessResult<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;

            var existingDispute = await disputeRepository.GetOpenDisputeForOrderAsync(
                request.OrderId, cancellationToken);

            if (existingDispute != null)
            {
                return BusinessResult.Failure<string>(new Error(nameof(request.OrderId), BusinessErrorMessage.DisputeAlreadyExists));
            }

            var dispute = new Dispute(
                orderId: request.OrderId,
                userId: userId,
                reason: request.Reason,
                description: request.Description,
                createdBy: userId
            );

            disputeRepository.Add(dispute);

            return BusinessResult.Success(dispute.Id);
        }
    }
}
