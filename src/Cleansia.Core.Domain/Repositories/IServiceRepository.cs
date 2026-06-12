using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Repositories;

public interface IServiceRepository : IRepository<Service, string>
{
    Task<bool> IsInUseAsync(string serviceId, CancellationToken cancellationToken);
}