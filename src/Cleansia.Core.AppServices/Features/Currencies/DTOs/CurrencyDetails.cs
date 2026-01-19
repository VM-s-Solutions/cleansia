namespace Cleansia.Core.AppServices.Features.Currencies.DTOs;

public record CurrencyDetailDto(
    string Id,
    string Code,
    string Name,
    string Symbol,
    decimal ExchangeRate,
    bool IsDefault);