using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Resolves THE currency an employee is paid, quoted and reported in — every partner-facing money
/// aggregate is scoped to it, and every partner screen labels its figures with its code.
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
/// Returns the ENTITY, not a code: callers filter rows on Currency.Id and label with Currency.Code,
/// and handing out only the code forced every caller to look the row up again. Never null — the
/// global default is the last link, and <c>ICurrencyRepository.GetDefaultAsync</c> throws rather than
/// returning nothing, so a platform with no default fails loudly here instead of labelling money
/// with an empty string. An unapproved employee (no WorkCountryId) resolves to that default.
/// </summary>
public interface ICurrencyResolutionService
{
    Task<Currency> ResolveCurrencyForEmployeeAsync(
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
