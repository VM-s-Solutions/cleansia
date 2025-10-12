using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Repositories;

public interface ICurrencyRepository : IRepository<Currency, string>
{
    Task<Currency> GetDefaultAsync(CancellationToken cancellationToken);
    Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken);
}