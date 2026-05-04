using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Loyalty.Admin;

/// <summary>
/// Admin manual loyalty revocation — the negative-points mirror of
/// <see cref="GrantPointsManually"/>. No-op when the user has no loyalty
/// account (nothing to take away).
/// </summary>
public class RevokePointsManually
{
    public record Command(
        string UserId,
        int Points,
        string Reason,
        string ActorId = "") : ICommand<Response>;

    public record Response(string UserId, int Points);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.UserNotFound);

            RuleFor(x => x.Points)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0)
                .WithMessage(BusinessErrorMessage.MustBePositive)
                .LessThanOrEqualTo(100_000)
                .WithMessage(BusinessErrorMessage.LoyaltyPointsExceedSanityCap);

            RuleFor(x => x.Reason)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.LoyaltyReasonRequired)
                .MaximumLength(500)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(ILoyaltyService loyaltyService) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            await loyaltyService.RevokePointsManuallyAsync(
                userId: command.UserId,
                points: command.Points,
                source: LoyaltyEarnSource.ManualGrant,
                orderId: null,
                actorId: command.ActorId,
                cancellationToken: cancellationToken);

            return BusinessResult.Success(new Response(command.UserId, command.Points));
        }
    }
}
