using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class PackagePriceRepository(CleansiaDbContext context) : BaseRepository<PackagePrice>(context), IPackagePriceRepository;
