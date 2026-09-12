namespace Cleansia.Core.AppServices.Features.Currencies.DTOs;

public record CurrencyDetailDto(
    string Id,
    string Code,
    string Name,
    string Symbol,
    bool IsDefault);

/// <summary>
/// The admin edit form's read shape. A separate record for the same reason <see cref="AdminCurrencyListItem"/>
/// is one: <see cref="CurrencyDetailDto"/> travels inside every order and dispute row on five contracts,
/// and a loyalty rate is not a fact about an order.
/// </summary>
public record AdminCurrencyDetailDto(
    string Id,
    string Code,
    string Name,
    string Symbol,
    bool IsDefault,
    bool IsActive,
    decimal? LoyaltyPointsDivisor);
