using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Infra.Database.Repositories;

public class ServiceCategoryRepository(CleansiaDbContext context) : BaseRepository<ServiceCategory>(context), IServiceCategoryRepository;
