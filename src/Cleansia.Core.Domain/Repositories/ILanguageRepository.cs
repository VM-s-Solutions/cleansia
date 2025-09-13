using Cleansia.Core.Domain.Internalization;

namespace Cleansia.Core.Domain.Repositories;

public interface ILanguageRepository : IRepository<Language, string>
{
    Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken);
}