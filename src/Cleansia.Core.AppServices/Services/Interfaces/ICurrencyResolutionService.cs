using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Resolves THE currency an employee is paid, quoted and reported in — every partner-facing money
/// aggregate is scoped to it, and every partner screen labels its figures with its code.
///
/// Source of truth chain:
///   Employee.WorkCountryId
///     -> CountryConfiguration.DefaultCurrencyCode
///        -> the Currency that code names
///
/// A cleaner is paid in the currency of the country they work in, and NOTHING is guessed (owner
/// ruling 2026-09-12, "throw instead 100%"). Every link is required: an employee that does not exist
/// or has no work country, a work country with no configuration, a blank code, or a code that names
/// no Currency row throws <see cref="InvalidOperationException"/> naming the country and the code.
/// <c>ApproveEmployee</c> is the only writer of <c>Approved</c> and refuses without a serviced work
/// country, so a working cleaner always has one; a cleaner with none has no pay to label.
///
/// The middle step is a LOOKUP, not a pass-through: <c>DefaultCurrencyCode</c> is free text with no
/// foreign key, so a typo, or a currency deleted after the country was configured, is a defect this
/// resolver surfaces rather than a label it hands out. A code naming an INACTIVE currency still
/// resolves: EUR is seeded real-but-not-yet-operated, and a country configured for it answers EUR;
/// whether that currency is offerable is the offerability gate's question, not this one's.
///
/// Returns the ENTITY, not a code: callers filter rows on Currency.Id and label with Currency.Code,
/// and handing out only the code forced every caller to look the row up again. Never null.
/// </summary>
public interface ICurrencyResolutionService
{
    /// <summary>Customer perk lookup pinned to their completed-order relationship with the cleaner.</summary>
    Task<Currency?> ResolveCurrencyForServingEmployeeAsync(string userId, string employeeId, CancellationToken cancellationToken);

    Task<Currency> ResolveCurrencyForEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The same chain entered one step down — at a country rather than the employee. A country is
    /// the ONE market signal the platform has on either side: a cleaner is paid in the currency of the
    /// country they work in, and a booking is priced and charged in the currency of the country its
    /// service address is in (owner rulings 2026-09-12). A NAMED country resolves to its configured
    /// currency or throws, exactly as above. Only a NULL country returns the platform default: that is
    /// the customer wizard before an address is known, the one legitimate case of a booking with no
    /// country yet. The default currency is required to exist and the repository throws when it does
    /// not.
    /// </summary>
    Task<Currency> ResolveCurrencyForCountryAsync(
        string? countryId,
        CancellationToken cancellationToken);
}
