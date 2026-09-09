namespace Cleansia.Core.AppServices.Features.Currencies.DTOs;

public record CurrencyDetailDto(
    string Id,
    string Code,
    string Name,
    string Symbol,
    bool IsDefault);