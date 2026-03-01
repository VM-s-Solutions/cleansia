using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IGdprRequestRepository : IRepository<GdprRequest, string>
{
    Task<List<GdprRequest>> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
    Task<bool> HasPendingRequestAsync(string userId, string requestType, CancellationToken cancellationToken);
}
