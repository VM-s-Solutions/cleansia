using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class CountryRepository(CleansiaDbContext context) : BaseRepository<Country>(context), ICountryRepository;