namespace Cleansia.Core.AppServices.Features.Currencies.DTOs;

public record CurrencyListItem(
    string Id,
    string Code,
    string Symbol,
    string Name,
    decimal ExchangeRate,
    bool IsDefault);