using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.LiveActivities;

/// <summary>
/// The client-side end path (ADR-0029 D3): the user dismissed the activity, so its update token is
/// gone — deletes the order-scoped row for the caller's device. Idempotent: an absent row is success
/// (the activity is already unregistered). <c>UserId</c> is taken from the session (S1).
/// </summary>
public static class UnregisterLiveActivity
{
    public record Command(string OrderId, string DeviceId) : ICommand<Response>;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.DeviceId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    internal class Handler(
        ILiveActivityTokenRepository liveActivityTokenRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(userId), BusinessErrorMessage.UserNotFound));
            }

            var token = await liveActivityTokenRepository
                .GetByUserDeviceOrderAsync(userId, command.DeviceId, command.OrderId, cancellationToken);

            if (token is not null)
            {
                liveActivityTokenRepository.Remove(token);
            }

            return BusinessResult.Success(new Response(true));
        }
    }

    public record Response(bool Success);
}
