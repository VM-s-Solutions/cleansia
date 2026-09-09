using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.Domain.Repositories;

public interface ICurrencyRepository : IRepository<Currency, string>
{
    Task<Currency> GetDefaultAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clears IsDefault on every row that carries it. Mirrors
    /// <c>ISavedAddressRepository.ClearDefaultForUserAsync</c>, and it clears ALL rather than "the"
    /// default on purpose: reading one row first and clearing that one leaves the promote racing a
    /// snapshot, and cannot recover a database that somehow has none.
    /// </summary>
    Task ClearDefaultAsync(CancellationToken cancellationToken);
    Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> ExistsWithCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> IsInUseAsync(string currencyId, CancellationToken cancellationToken);
}