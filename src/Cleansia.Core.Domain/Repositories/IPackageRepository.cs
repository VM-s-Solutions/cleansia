using Cleansia.Core.Domain.Packages;

namespace Cleansia.Core.Domain.Repositories;

public interface IPackageRepository : IRepository<Package, string>
{
    Task<bool> IsInUseAsync(string packageId, CancellationToken cancellationToken);
}