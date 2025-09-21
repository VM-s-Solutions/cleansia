using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Mappers;

public static class CurrencyMappers
{
    public static CurrencyListItem MapToDto(this Currency currency)
    {
        return new CurrencyListItem(
            Id: currency.Id,
            Code: currency.Code,
            Symbol: currency.Symbol,
            Name: currency.Name,
            ExchangeRate: currency.ExchangeRate);
    }
}