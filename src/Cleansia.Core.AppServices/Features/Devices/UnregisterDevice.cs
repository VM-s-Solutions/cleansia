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

            return BusinessResult.Success(new Response(true));
        }
    }

    public record Response(bool Success);
}
