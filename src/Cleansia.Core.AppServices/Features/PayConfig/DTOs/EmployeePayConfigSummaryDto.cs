namespace Cleansia.Core.AppServices.Features.PayConfig.DTOs;

public record EmployeePayConfigSummaryItemDto(
    string? ConfigId,
    string? ServiceId,
    string? ServiceName,
    string? PackageId,
    string? PackageName,
    bool HasConfig,
    decimal BasePay,
    decimal ExtraPerRoom,
    decimal ExtraPerBathroom,
    decimal DistanceRatePerKm,
    decimal MinimumPay,
    decimal MaximumPay,
    string? CurrencyId,
    string? CurrencyCode);

public record EmployeePayConfigSummaryDto(
    string EmployeeId,
    int TotalServices,
    int TotalPackages,
    int ConfiguredServices,
    int ConfiguredPackages,
    List<EmployeePayConfigSummaryItemDto> Services,
    List<EmployeePayConfigSummaryItemDto> Packages);
