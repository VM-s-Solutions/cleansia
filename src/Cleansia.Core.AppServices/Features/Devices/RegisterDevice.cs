using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Devices;

public static class RegisterDevice
{
    public record Command(
        string DeviceId,
        // Optional: a device can register BEFORE the OS grants push permission (or before APNs
        // provisioning exists at all) — the row then carries an empty token, shows up on the
        // Devices page and is revocable, and the push dispatcher skips it until a later
        // re-register upgrades it with a real token.
        string? DeviceToken,
        string Platform
    ) : ICommand<Response>;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DeviceId).NotEmpty();
            RuleFor(x => x.Platform).NotEmpty().Must(p => p is "android" or "ios").WithMessage(BusinessErrorMessage.InvalidPlatform);
        }
    }

    internal class Handler(
        IDeviceRepository deviceRepository,
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

            // Include inactive rows: a prior logout soft-deletes the device but leaves
            // it under the unique (UserId, DeviceId) index, so re-registration must
            // reclaim (reactivate) that row rather than INSERT a colliding duplicate.
            var existingDevice = await deviceRepository.GetByUserAndDeviceIdIncludingInactiveAsync(userId, request.DeviceId, cancellationToken);

            // Blank and null both mean token-less; the column is non-null, so "" is the
            // canonical stored form (the dispatcher's IsNullOrEmpty filter treats it as absent).
            var deviceToken = string.IsNullOrWhiteSpace(request.DeviceToken) ? string.Empty : request.DeviceToken;

            if (existingDevice is not null)
            {
                existingDevice.MarkRegistered(deviceToken);
                return BusinessResult.Success(new Response(existingDevice.Id));
            }

            var device = Device.Create(userId, request.Platform, deviceToken, request.DeviceId);
            deviceRepository.Add(device);

            return BusinessResult.Success(new Response(device.Id));
        }
    }

    public record Response(string DeviceId);
}
