using Cleansia.Core.AppServices.Features.Devices.DTOs;
using Cleansia.Core.Domain.Devices;

namespace Cleansia.Core.AppServices.Features.Devices.Mappers;

public static class DeviceMapper
{
    public static DeviceDto MapToDto(this Device device, string? currentDeviceId) =>
        new(
            Id: device.Id,
            Platform: device.Platform,
            DeviceId: device.DeviceId,
            LastActiveAt: device.LastActiveAt,
            IsCurrent: currentDeviceId is not null && device.DeviceId == currentDeviceId);
}
