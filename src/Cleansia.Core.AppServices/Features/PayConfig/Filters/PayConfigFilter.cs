namespace Cleansia.Core.AppServices.Features.PayConfig.Filters;

public record PayConfigFilter(
    string? EmployeeId,
    string? ServiceId,
    string? PackageId,
    string? CurrencyId);
