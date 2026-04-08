namespace Cleansia.Core.AppServices.Features.PayConfig.DTOs;

public record EmployeePayConfigDto(
    string Id,
    string? EmployeeId,
    string? EmployeeName,
    string? ServiceId,
    string? ServiceName,
    string? PackageId,
    string? PackageName,
    decimal BasePay,
    decimal ExtraPerRoom,
    decimal ExtraPerBathroom,
    decimal DistanceRatePerKm,
    decimal MinimumPay,
    decimal MaximumPay,
    string CurrencyId,
    string CurrencyCode,
    string? Description,
    DateTime CreatedOn);
