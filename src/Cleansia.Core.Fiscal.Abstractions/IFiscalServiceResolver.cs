namespace Cleansia.Core.Fiscal.Abstractions;

/// <summary>
/// Resolves the appropriate <see cref="IFiscalService"/> for a given country.
/// Falls back to the no-op implementation when no country-specific service is registered.
/// </summary>
public interface IFiscalServiceResolver
{
    /// <summary>
    /// Returns the fiscal service for the given ISO country code.
    /// Always returns a non-null service — falls back to a no-op implementation
    /// if no country-specific service is registered.
    /// </summary>
    /// <param name="isoCountryCode">ISO 3166-1 alpha-2 country code (e.g., "CZ", "SK").</param>
    IFiscalService Resolve(string isoCountryCode);
}
