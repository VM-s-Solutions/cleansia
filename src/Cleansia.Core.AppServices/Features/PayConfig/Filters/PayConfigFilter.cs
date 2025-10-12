namespace Cleansia.Core.AppServices.Features.PayConfig.Filters;

public record PayConfigFilter(
    string? ServiceId,
    string? PackageId,
    string? CurrencyId);
