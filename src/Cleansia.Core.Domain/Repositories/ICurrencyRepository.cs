using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Repositories;

public interface ICurrencyRepository : IRepository<Currency, string>
{
    Task<Currency> GetDefaultAsync(CancellationToken cancellationToken);
    Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> IsInUseAsync(string currencyId, CancellationToken cancellationToken);
}