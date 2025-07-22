using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Infra.Database.Repositories;

public class ServiceRepository(CleansiaDbContext context) : BaseRepository<Service>(context), IServiceRepository;