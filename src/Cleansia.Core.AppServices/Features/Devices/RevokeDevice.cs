using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Devices;

public static class RevokeDevice
{
    public record Command(string DeviceRowId) : ICommand<Response>;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DeviceRowId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    internal class Handler(
        IDeviceRepository deviceRepository,
        IRefreshTokenService refreshTokenService,
        ILiveActivityTokenRepository liveActivityTokenRepository,
        IUserSessionProvider userSessionProvider
    ) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()
                         ?? throw new UnauthorizedAccessException("User ID not found in claims.");

            var device = await deviceRepository.GetByIdAndUserAsync(request.DeviceRowId, userId, cancellationToken);
            if (device is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(Command.DeviceRowId), BusinessErrorMessage.DeviceNotFound));
            }

            deviceRepository.Deactivate(device);
            await refreshTokenService.RevokeByDeviceAsync(userId, device.DeviceId, "device_revoked", cancellationToken);

            // A revoked device must stop receiving lock-screen order state (ADR-0029 D3 / ADR-0026
            // revocation intent): hard-delete its activity tokens, push-to-start included.
            var activityTokens = await liveActivityTokenRepository.GetByUserAndDeviceAsync(userId, device.DeviceId, cancellationToken);
            if (activityTokens.Count > 0)
            {
                liveActivityTokenRepository.RemoveRange(activityTokens);
            }

            return BusinessResult.Success(new Response(true));
        }
    }

    public record Response(bool Success);
}
