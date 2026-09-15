using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IGdprRequestRepository : IRepository<GdprRequest, string>
{
    Task<List<GdprRequest>> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the subject has a request of this type that is not yet <c>Completed</c>. A <c>Failed</c>
    /// row counts: the retry sweep or an admin finishes it, and a second filing that completed on a row of
    /// its own would leave two Completed rows for one erasure once the sweep re-walked the erased subject.
    /// </summary>
    Task<bool> HasPendingRequestAsync(string userId, string requestType, CancellationToken cancellationToken);
}
