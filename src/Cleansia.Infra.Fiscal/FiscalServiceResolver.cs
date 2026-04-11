using Cleansia.Core.Fiscal.Abstractions;
using Cleansia.Infra.Fiscal.NoOp;

namespace Cleansia.Infra.Fiscal;

public class FiscalServiceResolver : IFiscalServiceResolver
{
    private readonly IReadOnlyDictionary<string, IFiscalService> _servicesByCountry;
    private readonly IFiscalService _fallback;

    public FiscalServiceResolver(
        IEnumerable<IFiscalService> services,
        NoOpFiscalService fallback)
    {
        _fallback = fallback;

        // Only non-fallback services go into the lookup.
        // The fallback is used when no country-specific service is registered.
        _servicesByCountry = services
            .Where(s => s is not NoOpFiscalService)
            .GroupBy(s => s.CountryCode.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Last());
    }

    public IFiscalService Resolve(string isoCountryCode)
    {
        if (string.IsNullOrWhiteSpace(isoCountryCode))
        {
            return _fallback;
        }

        var key = isoCountryCode.ToUpperInvariant();
        return _servicesByCountry.TryGetValue(key, out var service) ? service : _fallback;
    }
}
