using Cleansia.Core.Domain.Internalization;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class LanguageRepository(CleansiaDbContext context) : BaseRepository<Language>(context), ILanguageRepository;