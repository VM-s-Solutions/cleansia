using Cleansia.Core.AppServices.Abstractions;
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
            var userId = userSessionProvider.GetUserId()
                         ?? throw new UnauthorizedAccessException("User ID not found in claims.");

            var device = await deviceRepository.GetByUserAndDeviceIdAsync(userId, request.DeviceId, cancellationToken);

            if (device is not null)
            {
                deviceRepository.Remove(device);
            }

            return BusinessResult.Success(new Response(true));
        }
    }

    public record Response(bool Success);
}
