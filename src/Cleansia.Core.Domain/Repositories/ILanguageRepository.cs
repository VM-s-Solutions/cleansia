using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Repositories;

public interface ILanguageRepository : IRepository<Language, string>
{
    Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken);
    Task<Language?> GetByCodeAsync(string code, CancellationToken cancellationToken);
}