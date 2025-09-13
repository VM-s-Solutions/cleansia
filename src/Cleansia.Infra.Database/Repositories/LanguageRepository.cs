using Cleansia.Core.Domain.Internalization;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class LanguageRepository(CleansiaDbContext context) : BaseRepository<Language>(context), ILanguageRepository
{
    public Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(l => l.Code == code, cancellationToken);
    }
}