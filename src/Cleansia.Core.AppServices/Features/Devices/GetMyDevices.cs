using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Devices.DTOs;
using Cleansia.Core.AppServices.Features.Devices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Devices;

public class GetMyDevices
{
    public record Query(string? CurrentDeviceId = null) : IQuery<IReadOnlyList<DeviceDto>>;

    internal class Handler(
        IDeviceRepository deviceRepository,
        IUserSessionProvider userSessionProvider) : IQueryHandler<Query, IReadOnlyList<DeviceDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<DeviceDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()
                         ?? throw new UnauthorizedAccessException("User ID not found in claims.");

            var devices = await deviceRepository.GetByUserIdAsync(userId, cancellationToken);

            var dtos = devices
                .Select(device => device.MapToDto(query.CurrentDeviceId))
                .ToList();

            return BusinessResult.Success<IReadOnlyList<DeviceDto>>(dtos);
        }
    }
}
