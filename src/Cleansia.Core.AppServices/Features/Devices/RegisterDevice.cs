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
        string DeviceToken,
        string Platform
    ) : ICommand<Response>;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DeviceId).NotEmpty();
            RuleFor(x => x.DeviceToken).NotEmpty();
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
            var userId = userSessionProvider.GetUserId()
                         ?? throw new UnauthorizedAccessException("User ID not found in claims.");

            var existingDevice = await deviceRepository.GetByUserAndDeviceIdAsync(userId, request.DeviceId, cancellationToken);

            if (existingDevice is not null)
            {
                existingDevice.UpdateToken(request.DeviceToken);
                return BusinessResult.Success(new Response(existingDevice.Id));
            }

            var device = Device.Create(userId, request.Platform, request.DeviceToken, request.DeviceId);
            deviceRepository.Add(device);

            return BusinessResult.Success(new Response(device.Id));
        }
    }

    public record Response(string DeviceId);
}
