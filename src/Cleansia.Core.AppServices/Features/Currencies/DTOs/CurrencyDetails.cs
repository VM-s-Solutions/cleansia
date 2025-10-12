namespace Cleansia.Core.AppServices.Features.Currencies.DTOs;

public record CurrencyDetails(
    string Id,
    string Code,
    string Name,
    string Symbol
);