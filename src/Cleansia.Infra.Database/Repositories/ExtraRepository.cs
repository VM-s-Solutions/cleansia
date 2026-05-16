using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class ExtraRepository(CleansiaDbContext context) : BaseRepository<Extra>(context), IExtraRepository;
