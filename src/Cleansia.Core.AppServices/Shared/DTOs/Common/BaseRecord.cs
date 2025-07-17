namespace Cleansia.Core.AppServices.Shared.DTOs.Common;

public record BaseRecord(
    int Id,
    bool IsActive);

public record BaseRecord<T>(
    T Id,
    bool IsActive);