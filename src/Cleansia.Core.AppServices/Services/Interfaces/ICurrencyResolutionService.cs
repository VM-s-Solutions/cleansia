namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Resolves the currency an employee should be paid / quoted in.
///
/// Source of truth chain:
///   Employee.WorkCountryId
///     -> CountryConfiguration.DefaultCurrencyCode
///        -> Currency (global default)
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
}
