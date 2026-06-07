using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Loyalty.Admin;

public class GrantPointsManually
{
    /// <summary>
    /// <paramref name="RequestId"/> (S7a) is a REQUIRED client-generated
    /// idempotency token. The admin client generates it ONCE per logical grant attempt and resends the
    /// SAME value on a double-submit / proxy-retry / network-retry; the service persists it as the
    /// ledger row's <c>IdempotencyKey</c> and a filtered UNIQUE INDEX collapses the retry onto exactly
    /// one grant (so points — which drive tier discounts — are never doubled). A genuine new grant uses
    /// a new token. NOTE: until the admin UI adopts it, callers must supply a value (the field is
    /// internal to the backend contract); the NSwag admin client drifts on this DTO shape — flagged
    /// nswag-regen.
    /// </summary>
    public record Command(
        string UserId,
        int Points,
        string Reason,
        string RequestId) : ICommand<Response>;

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

            // S7a — the idempotency token must be present and bounded to the persisted
            // IdempotencyKey column width (80).
            RuleFor(x => x.RequestId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(80)
                .WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        ILoyaltyService loyaltyService,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            await loyaltyService.GrantPointsManuallyAsync(
                userId: command.UserId,
                points: command.Points,
                source: LoyaltyEarnSource.ManualGrant,
                orderId: null,
                actorId: actorId,
                reason: command.Reason,
                // S7a: thread the client idempotency token into the service so a retry
                // collapses onto one ledger row. The service returns the same success either way.
                requestId: command.RequestId,
                cancellationToken: cancellationToken);

            return BusinessResult.Success(new Response(command.UserId, command.Points));
        }
    }
}
