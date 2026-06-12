namespace Cleansia.Core.AppServices.Features.Devices.DTOs;

public record DeviceDto(
    string Id,
    string Platform,
    string DeviceId,
    DateTimeOffset LastActiveAt,
    bool IsCurrent
);
