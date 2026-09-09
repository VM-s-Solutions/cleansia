using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Infra.Database.Repositories;

public class ServicePriceRepository(CleansiaDbContext context) : BaseRepository<ServicePrice>(context), IServicePriceRepository;
