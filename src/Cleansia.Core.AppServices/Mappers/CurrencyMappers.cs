using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Mappers;

/// <summary>
/// Pure projections. The admin shapes add <c>IsActive</c> and <c>LoyaltyPointsDivisor</c>; no rate is
/// mapped anywhere because none exists.
/// </summary>
public static class CurrencyMappers
{
    public static CurrencyListItem MapToDto(this Currency currency)
    {
        return new CurrencyListItem(
            Id: currency.Id,
            Code: currency.Code,
            Symbol: currency.Symbol,
            Name: currency.Name,
            IsDefault: currency.IsDefault);
    }

    /// <summary>Admin only — see <see cref="AdminCurrencyListItem"/> for why it is not the same DTO.</summary>
    public static AdminCurrencyListItem MapToAdminListItem(this Currency currency)
    {
        return new AdminCurrencyListItem(
            Id: currency.Id,
            Code: currency.Code,
            Symbol: currency.Symbol,
            Name: currency.Name,
            IsDefault: currency.IsDefault,
            IsActive: currency.IsActive,
            LoyaltyPointsDivisor: currency.LoyaltyPointsDivisor);
    }

    /// <summary>Admin only — see <see cref="AdminCurrencyDetailDto"/>.</summary>
    public static AdminCurrencyDetailDto MapToAdminDetailDto(this Currency currency)
    {
        return new AdminCurrencyDetailDto(
            Id: currency.Id,
            Code: currency.Code,
            Name: currency.Name,
            Symbol: currency.Symbol,
            IsDefault: currency.IsDefault,
            IsActive: currency.IsActive,
            LoyaltyPointsDivisor: currency.LoyaltyPointsDivisor);
    }

    public static CurrencyDetailDto MapToDetailDto(this Currency currency)
    {
        return new CurrencyDetailDto(
            Id: currency.Id,
            Code: currency.Code,
            Name: currency.Name,
            Symbol: currency.Symbol,
            IsDefault: currency.IsDefault);
    }
}