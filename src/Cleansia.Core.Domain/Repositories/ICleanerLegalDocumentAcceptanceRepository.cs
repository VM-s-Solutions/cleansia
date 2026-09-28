using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Adds, reads by cleaner, and two pseudonymising writes — nothing that updates or removes a row by hand.
/// → <see cref="CleanerLegalDocumentAcceptance"/>
/// </summary>
public interface ICleanerLegalDocumentAcceptanceRepository : IRepository<CleanerLegalDocumentAcceptance, string>
{
    /// <summary>One cleaner's rows in every operating company, newest first, for the subject export.</summary>
    Task<IReadOnlyList<CleanerLegalDocumentAcceptance>> GetByEmployeeIdNoTrackingAsync(string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the cleaner's rows TRACKED, past the tenant filter, and pseudonymises each so the blanking
    /// rides the erasure's single commit. Returns the number of rows touched.
    /// </summary>
    Task<int> PseudonymiseForEmployeeAsync(string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// Blanks the request metadata on every row of the AMBIENT company accepted before the cutoff that
    /// still carries any, in batches, committing each. Never deletes. Returns the number of rows touched.
    /// </summary>
    Task<int> PseudonymiseExpiredAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken);
}
