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
        private readonly IOrderRepository _orderRepository;
        private readonly IUserRepository _userRepository;

        public Validator(IOrderRepository orderRepository, IUserRepository userRepository)
        {
            _orderRepository = orderRepository;
            _userRepository = userRepository;

            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(_userRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.UserNotFound);

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
        string Description,
        string UserId = ""
    ) : ICommand<string>;

    public class Handler : ICommandHandler<Command, string>
    {
        private readonly IDisputeRepository _disputeRepository;

        public Handler(IDisputeRepository disputeRepository)
        {
            _disputeRepository = disputeRepository;
        }

        public async Task<BusinessResult<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Check if dispute already exists for this order
            var existingDispute = await _disputeRepository
                .GetDisputesByOrderId(request.OrderId)
                .Where(d => d.Status != DisputeStatus.Closed)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingDispute != null)
            {
                return BusinessResult.Failure<string>(new Error(nameof(request.OrderId), BusinessErrorMessage.DisputeAlreadyExists));
            }

            var dispute = new Dispute(
                orderId: request.OrderId,
                userId: request.UserId,
                reason: request.Reason,
                description: request.Description,
                createdBy: request.UserId
            );

            _disputeRepository.Add(dispute);

            return BusinessResult.Success(dispute.Id);
        }
    }
}
