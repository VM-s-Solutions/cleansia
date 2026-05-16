using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Cleansia.Infra.Database;

public abstract class BaseRepository<TEntity>(CleansiaDbContext context) : IRepository<TEntity, string>
    where TEntity : class, IEntity<string>
{
    public CleansiaDbContext Context { get; private set; } = context;

    public virtual Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(entity => entity.Id!.Equals(id), cancellationToken);
    }

    public async Task<bool> ExistWithIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        ids = ids.Distinct().ToArray();
        var count = await GetQueryable()
            .Where(e => ids.Contains(e.Id))
            .CountAsync(cancellationToken);

        return count == ids.Count();
    }

    public virtual Task<TEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var query = GetQueryable();
        return query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)!;
    }

    public virtual IQueryable<TEntity> GetByIds(IEnumerable<string> ids)
    {
        ids = ids.Distinct().ToArray();
        var query = GetQueryable();
        return query.Where(x => ids.Contains(x.Id));
    }

    public virtual IQueryable<TEntity> GetPaged(int offset, int limit)
    {
        var query = GetQueryable();
        return query.Skip(offset).Take(limit);
    }

    public virtual IQueryable<TEntity> GetPaged(int offset, int limit, Expression<Func<TEntity, bool>> filter)
    {
        var query = FilterData(filter);
        return query.Skip(offset).Take(limit);
    }

    public virtual IQueryable<TEntity> GetPagedSort<TSort>(int offset, int limit, Expression<Func<TEntity, bool>> filter, SortDefinition sort)
        where TSort : BaseSort<TEntity>
    {
        var query = FilterData(filter);

        var sortField = sort.Field?.ToLower();
        var sortByAsc = sort?.Direction is null or SortDirection.Ascending;
        var entitySort = Activator.CreateInstance(typeof(TSort), sortField, sortByAsc) as TSort;
        query = entitySort!.ApplySort(query);

        return query.Skip(offset).Take(limit);
    }

    public virtual IQueryable<TEntity> GetPagedSort<TSort>(int offset, int limit, Expression<Func<TEntity, bool>>? filter, IEnumerable<SortDefinition> sortDefinitions)
        where TSort : BaseSort<TEntity>
    {
        var query = FilterData(filter);

        foreach (var (sort, index) in sortDefinitions.Select((value, i) => (value, i)))
        {
            var sortField = sort.Field?.ToLower();
            var sortByAsc = sort?.Direction is null or SortDirection.Ascending;
            var entitySort = Activator.CreateInstance(typeof(TSort), sortField, sortByAsc) as TSort;
            query = entitySort!.ApplySort(query, index != 0);
        }

        return query.Skip(offset).Take(limit);
    }

    public virtual async Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        var query = GetQueryable();
        return await query.CountAsync(cancellationToken);
    }

    public virtual async Task<int> GetCountAsync(Expression<Func<TEntity, bool>>? filter, CancellationToken cancellationToken)
    {
        var query = FilterData(filter);
        return await query.CountAsync(cancellationToken);
    }

    public virtual IQueryable<TEntity> GetFiltered(Expression<Func<TEntity, bool>> filter)
    {
        var query = FilterData(filter);
        return query;
    }

    public virtual IQueryable<TEntity> GetAll()
    {
        return GetQueryable();
    }

    public virtual void Add(TEntity entity)
    {
        Context.Add(entity);
    }

    public virtual void AddRange(IEnumerable<TEntity> entities)
    {
        Context.AddRange(entities);
    }

    public virtual void Deactivate(TEntity entity)
    {
        entity.IsActive = false;
    }

    public virtual void DeactivateRange(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (entity is BaseEntity)
            {
                entity.IsActive = false;
            }
        }
    }

    public virtual void Remove(TEntity entity)
    {
        Context.Remove(entity);
    }

    public virtual void RemoveRange(IEnumerable<TEntity> entities)
    {
        GetDbSet().RemoveRange(entities);
    }

    public virtual IQueryable<TEntity> GetQueryable()
    {
        return GetDbSet();
    }

    public virtual IQueryable<TEntity> GetQueryableIgnoringTenant()
    {
        return GetDbSet().IgnoreQueryFilters();
    }

    protected DbSet<TEntity> GetDbSet() => Context.Set<TEntity>();

    protected IQueryable<TEntity> FilterData(Expression<Func<TEntity, bool>>? filter)
    {
        var query = GetQueryable();
        if (filter is not null)
        {
            query = query.Where(filter);
        }

        return query;
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await Context.CommitAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return Context.BeginTransactionAsync(cancellationToken);
    }

    public void Rollback()
    {
        Context.Rollback();
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}