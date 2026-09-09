using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Mappers;

/// <summary>
/// <b><c>ExchangeRate</c> is reported as 1 from every mapper here, not read from the column.</b>
///
/// <para>Nothing converts any more: the pricing calculator's fourteen scaling sites were removed under
/// the owner ruling that a price is AUTHORED per currency, never converted. The column still exists
/// only because the field is on two committed mobile contracts that treat it as required, and it is
/// deleted with the per-currency price tables.</para>
///
/// <para>Surfacing the stored number in the meantime made the same named field disagree between the
/// quote (which reports 1) and the order list (which reported whatever an admin had typed), for the
/// same order. Reporting 1 everywhere is the honest answer to "what rate was applied": none was.</para>
/// </summary>
public static class CurrencyMappers
{
    private const decimal NoConversionApplied = 1m;

    public static CurrencyListItem MapToDto(this Currency currency)
    {
        return new CurrencyListItem(
            Id: currency.Id,
            Code: currency.Code,
            Symbol: currency.Symbol,
            Name: currency.Name,
            ExchangeRate: NoConversionApplied,
            IsDefault: currency.IsDefault);
    }

    public static CurrencyDetailDto MapToDetailDto(this Currency currency)
    {
        return new CurrencyDetailDto(
            Id: currency.Id,
            Code: currency.Code,
            Name: currency.Name,
            Symbol: currency.Symbol,
            ExchangeRate: NoConversionApplied,
            IsDefault: currency.IsDefault);
    }
}