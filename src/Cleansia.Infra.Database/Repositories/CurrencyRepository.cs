using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class CurrencyRepository(CleansiaDbContext context) : BaseRepository<Currency>(context), ICurrencyRepository;