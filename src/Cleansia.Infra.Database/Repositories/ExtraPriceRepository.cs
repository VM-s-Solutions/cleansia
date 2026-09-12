using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class ExtraPriceRepository(CleansiaDbContext context) : BaseRepository<ExtraPrice>(context), IExtraPriceRepository;
