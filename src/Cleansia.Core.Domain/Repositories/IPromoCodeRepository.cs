using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.Domain.Repositories;

public interface IPromoCodeRepository : IRepository<PromoCode, string>
{
    /// <summary>
    /// Lookup a code by its (canonical, uppercase) <see cref="PromoCode.Code"/>
    /// value. Tenant scoping is handled by the EF global query filter.
    /// </summary>
    Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Admin-side paged query — accepts optional flags for active/expired
    /// status and a code search prefix. Returns the materialised page plus
    /// the unfiltered total. Tenant scoping is handled by the EF global
    /// query filter.
    /// </summary>
    Task<(IReadOnlyList<PromoCode> Items, int Total)> GetPagedAdminAsync(
        bool? active,
        bool? expired,
        string? searchCode,
        int offset,
        int limit,
        CancellationToken cancellationToken);
}
