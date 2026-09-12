using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Packages;

/// <summary>
/// A package's price in ONE currency, authored by a human. See <see cref="ServicePrice"/> for why
/// prices are authored per currency rather than converted.
///
/// <para>One money column: a package's price is flat, and the split across its included services is
/// derived from their weights.</para>
/// </summary>
public class PackagePrice : Auditable
{
    public string PackageId { get; private set; }
    public Package? Package { get; private set; }

    public string CurrencyId { get; private set; }
    public Currency? Currency { get; private set; }

    public decimal Price { get; private set; }

    public static PackagePrice Create(string packageId, string currencyId, decimal price) => new()
    {
        PackageId = packageId,
        CurrencyId = currencyId,
        Price = price
    };

    public PackagePrice Update(decimal price)
    {
        Price = price;
        return this;
    }
}
