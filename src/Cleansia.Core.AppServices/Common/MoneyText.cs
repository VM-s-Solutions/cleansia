using System.Globalization;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Common;

/// <summary>
/// Money as a notification states it: the number in invariant culture with no trailing zeros, a
/// space, then the currency's own symbol ("250 Kč", "10 €", "9.5 zł"); the code stands in for a
/// currency with no symbol. Formatted on the server rather than on the device because the figure is
/// the row's own currency, which a client rendering the args cannot know from the order alone
/// (owner ruling 2026-09-13).
/// </summary>
public static class MoneyText
{
    public static string Format(decimal amount, Currency currency)
    {
        var number = amount.ToString("0.############################", CultureInfo.InvariantCulture);
        var unit = string.IsNullOrWhiteSpace(currency.Symbol) ? currency.Code : currency.Symbol;
        return $"{number} {unit}";
    }
}
