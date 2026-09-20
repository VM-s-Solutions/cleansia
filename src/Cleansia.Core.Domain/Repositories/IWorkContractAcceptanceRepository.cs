using Cleansia.Core.Domain.Contracts;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Adds, reads by seat, by order, by cleaner and by acceptance, and two pseudonymising writes — nothing
/// that updates or removes a row by hand. The archive bundle streams the table through the inherited
/// <c>GetQueryable()</c>. The inherited <c>Remove</c>/<c>Deactivate</c> members exist on every
/// repository and are pinned unused on this one by <c>WorkContractAcceptanceImmutabilityTests</c>.
/// → <see cref="WorkContractAcceptance"/>
/// </summary>
public interface IWorkContractAcceptanceRepository : IRepository<WorkContractAcceptance, string>
{
    /// <summary>
    /// One row by id past the tenant filter. The row is stamped with the ORDER's operator, and the
    /// order's customer may be booked across the border (their order reads are owner-pinned past the
    /// filter the same way); the caller pins access on the row's order afterwards, never on the id alone.
    /// </summary>
    Task<WorkContractAcceptance?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken);

    /// <summary>The rows of the named seats, past the tenant filter for the same reason — the seats are an authorized order's.</summary>
    Task<IReadOnlyList<WorkContractAcceptanceRow>> GetForSeatsAsync(IReadOnlyCollection<string> orderEmployeeIds, CancellationToken cancellationToken);

    /// <summary>The rows on the named orders, oldest first — the customer's export, pinned by the orders the export already listed.</summary>
    Task<IReadOnlyList<WorkContractAcceptanceRow>> GetForOrdersAsync(IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken);

    /// <summary>Whether the seat has its contract — the start and complete gates' question, in the caller's company.</summary>
    Task<bool> AnyForSeatAsync(string orderEmployeeId, CancellationToken cancellationToken);

    /// <summary>One cleaner's rows in every operating company, newest first, for the subject export.</summary>
    Task<IReadOnlyList<WorkContractAcceptanceRow>> GetByEmployeeIdNoTrackingAsync(string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the cleaner's rows TRACKED, past the tenant filter, and calls
    /// <see cref="WorkContractAcceptance.Pseudonymise"/> on each so the blanking rides the erasure's
    /// single commit. Returns the number of rows touched.
    /// </summary>
    Task<int> PseudonymiseForEmployeeAsync(string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// Blanks the trio on every row of the AMBIENT company accepted before the cutoff that still carries
    /// one, in batches, committing each. Never deletes. Returns the number of rows touched.
    /// </summary>
    Task<int> PseudonymiseExpiredAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken);
}
