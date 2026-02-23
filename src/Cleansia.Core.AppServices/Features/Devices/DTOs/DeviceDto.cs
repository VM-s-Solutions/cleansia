namespace Cleansia.Core.AppServices.Features.Devices.DTOs;

public record DeviceDto(
    string Id,
    string UserId,
    string Platform,
    string DeviceId,
    DateTimeOffset LastActiveAt
);
