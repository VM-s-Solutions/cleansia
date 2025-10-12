namespace Cleansia.Core.AppServices.Features.Services.DTOs;

public record ServiceDetails(
    string Id,
    string Name,
    string Description,
    int EstimatedTime,
    string CurrencyCode);