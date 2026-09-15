using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// An extra's price in ONE currency, authored by a human. See
/// <see cref="Cleansia.Core.Domain.Services.ServicePrice"/> for why prices are authored per currency
/// rather than converted.
/// </summary>
public class ExtraPrice : Auditable
{
    public string ExtraId { get; private set; }
    public Extra? Extra { get; private set; }

    public string CurrencyId { get; private set; }
    public Currency? Currency { get; private set; }

    public decimal Price { get; private set; }

    public static ExtraPrice Create(string extraId, string currencyId, decimal price) => new()
    {
        ExtraId = extraId,
        CurrencyId = currencyId,
        Price = price
    };

    public ExtraPrice Update(decimal price)
    {
        Price = price;
        return this;
    }
}
