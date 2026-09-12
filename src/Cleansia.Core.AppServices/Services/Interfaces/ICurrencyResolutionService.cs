using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Resolves the currency an employee should be paid / quoted in.
///
/// Source of truth chain:
///   Employee.WorkCountryId
///     -> CountryConfiguration.DefaultCurrencyCode, IF it names a real Currency
///        -> Currency (global default)
///
/// The middle step is a LOOKUP, not a pass-through. That column is free text with no foreign key, so
/// a code that names no currency — a typo, or one deleted after the country was configured — falls
/// through to the global default instead of being handed out as a label. A code naming an INACTIVE
/// currency still resolves: EUR is seeded real-but-not-yet-operated, and a country configured for it
/// should answer EUR the day it is switched on.
///
/// An unapproved employee (no WorkCountryId) falls back to the
/// platform's global default currency. Callers should treat a null
/// return as "use the global default" — the implementation already
/// applies that fallback internally but the contract stays nullable
/// so a missing global default doesn't silently mask configuration
/// bugs upstream.
/// </summary>
public interface ICurrencyResolutionService
{
    Task<string?> ResolveCurrencyCodeForEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The same chain entered one step down — at the work country rather than the employee — for the
    /// caller that has the country before the employee does: approval assigns <c>WorkCountryId</c> in
    /// the same command that must already know which currency the cleaner's board will be paid in.
    /// A null or unknown country, or a code naming no currency, resolves to the platform default,
    /// exactly as above. Never null: the default currency is required to exist and the repository
    /// throws when it does not.
    /// </summary>
    Task<Currency> ResolveCurrencyForWorkCountryAsync(
        string? workCountryId,
        CancellationToken cancellationToken);
}
