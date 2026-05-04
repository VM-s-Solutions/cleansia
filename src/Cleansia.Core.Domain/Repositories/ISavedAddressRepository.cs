using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface ISavedAddressRepository : IRepository<SavedAddress, string>
{
    Task<IReadOnlyList<SavedAddress>> GetByUserAsync(string userId, CancellationToken cancellationToken);
    Task<SavedAddress?> GetDefaultForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Clear the default flag on all of a user's saved addresses. Used before setting a new one.</summary>
    Task ClearDefaultForUserAsync(string userId, CancellationToken cancellationToken);
}
