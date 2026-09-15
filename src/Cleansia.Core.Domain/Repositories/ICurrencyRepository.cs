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

    /// <summary>
    /// Whether the platform can QUOTE in this currency: the row exists, it is switched on
    /// (<c>IsActive</c>) AND the catalogue carries at least one price row in it — a service, a package
    /// or an extra.
    ///
    /// <para>This is the ONE definition of an offerable currency. <c>QuoteOrder</c>, <c>CreateOrder</c>
    /// and <c>QuotePlusSavings</c> gate a caller-named <c>CurrencyId</c> on it, and
    /// <c>SetDefaultCurrency</c> gates promotion on it, so the set a customer can book in and the set
    /// an admin can star are the same set. It answers "has the catalogue been priced in it at all",
    /// not "is this selection priced in it" — the per-item question is the pricing calculator's, and
    /// that one fails closed.</para>
    /// </summary>
    Task<bool> IsOfferableAsync(string currencyId, CancellationToken cancellationToken);
}