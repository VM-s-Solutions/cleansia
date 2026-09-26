using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IAddressRepository : IRepository<Address, string>
{
    Task<Address?> GetAddressAsync(string street, string city, string zipCode, string countryId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of <paramref name="addressIds"/> something outside <paramref name="exceptOrderIds"/> still points
    /// at: another order, a saved address, or an employee. The saved addresses in
    /// <paramref name="exceptSavedAddressIds"/> and the employee <paramref name="exceptEmployeeId"/>
    /// do not count when the caller removes their references in the same commit. Read past the tenant
    /// filter, because a deduped row is shared whichever company is asking.
    /// </summary>
    Task<IReadOnlySet<string>> GetReferencedElsewhereAsync(
        IReadOnlyCollection<string> addressIds,
        IReadOnlyCollection<string> exceptOrderIds,
        IReadOnlyCollection<string> exceptSavedAddressIds,
        string? exceptEmployeeId,
        CancellationToken cancellationToken);
}
