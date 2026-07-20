using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Devices;

public static class UnregisterDevice
{
    public record Command(string DeviceId) : ICommand<Response>;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DeviceId).NotEmpty();
        }
    }

    internal class Handler(
        IDeviceRepository deviceRepository,
        ILiveActivityTokenRepository liveActivityTokenRepository,
        IUserSessionProvider userSessionProvider
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(userId), BusinessErrorMessage.UserNotFound));
            }

            var device = await deviceRepository.GetByUserAndDeviceIdAsync(userId, request.DeviceId, cancellationToken);

            if (device is not null)
            {
                deviceRepository.Deactivate(device);
            }

            // Sign-out on the device: hard-delete its activity tokens (push-to-start included) so a
            // signed-out handset stops receiving lock-screen order state (ADR-0029 D3). Keyed by the
            // client (userId, deviceId), so it runs even when the Device row is already gone.
            var activityTokens = await liveActivityTokenRepository.GetByUserAndDeviceAsync(userId, request.DeviceId, cancellationToken);
            if (activityTokens.Count > 0)
            {
                liveActivityTokenRepository.RemoveRange(activityTokens);
            }

            return BusinessResult.Success(new Response(true));
        }
    }

    public record Response(bool Success);
}
