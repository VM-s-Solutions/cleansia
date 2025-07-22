using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class PackageRepository(CleansiaDbContext context): BaseRepository<Package>(context), IPackageRepository;