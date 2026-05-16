using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.SeedWork;
using System.Linq.Expressions;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Repositories;

public interface IRepository<TEntity, in TKey> : IUnitOfWork
    where TEntity : IEntity<TKey>
{
    Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken);

    Task<bool> ExistWithIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken);

    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken);

    IQueryable<TEntity> GetByIds(IEnumerable<TKey> ids);

    IQueryable<TEntity> GetPaged(int offset, int limit);

    IQueryable<TEntity> GetPaged(int offset, int limit, Expression<Func<TEntity, bool>> filter);

    IQueryable<TEntity> GetPagedSort<TSort>(int offset, int limit, Expression<Func<TEntity, bool>> filter, SortDefinition sort)
        where TSort : BaseSort<TEntity>;

    IQueryable<TEntity> GetPagedSort<TSort>(int offset, int limit, Expression<Func<TEntity, bool>>? filter, IEnumerable<SortDefinition> sort)
        where TSort : BaseSort<TEntity>;

    Task<int> GetCountAsync(CancellationToken cancellationToken);

    Task<int> GetCountAsync(Expression<Func<TEntity, bool>>? filter, CancellationToken cancellationToken);

    IQueryable<TEntity> GetFiltered(Expression<Func<TEntity, bool>> filter);

    IQueryable<TEntity> GetAll();

    void Add(TEntity entity);

    void AddRange(IEnumerable<TEntity> entities);

    void Remove(TEntity entity);

    void RemoveRange(IEnumerable<TEntity> entities);

    void Deactivate(TEntity entity);

    void DeactivateRange(IEnumerable<TEntity> entities);

    IQueryable<TEntity> GetQueryable();

    /// <summary>
    /// Bypasses the tenant query filter. ONLY for system-level jobs
    /// (Azure Functions, BackgroundServices, Stripe webhooks) that have no JWT
    /// and need to enumerate or look up rows across all tenants. Callers that
    /// MUTATE rows MUST set <see cref="ITenantProvider.SetTenantOverride"/> from
    /// the loaded entity's TenantId before the change is persisted, so child
    /// rows inherit the right tenant.
    /// </summary>
    IQueryable<TEntity> GetQueryableIgnoringTenant();
}
