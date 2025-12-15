using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class LanguageRepository(CleansiaDbContext context) : BaseRepository<Language>(context), ILanguageRepository
{
    public Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(l => l.Code == code, cancellationToken);
    }

    public Task<Language?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(l => l.Code == code, cancellationToken);
    }
}