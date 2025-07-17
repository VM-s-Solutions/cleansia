using Cleansia.Core.Domain.Common;
using System.Linq.Expressions;

namespace Cleansia.Core.Domain.Sorting.Common;

public abstract class BaseSort<T>(string propertyName, bool isAscending)
    where T : IEntity
{
    private Expression<Func<T, object>>? defaultSort;

    public virtual Expression<Func<T, object>> DefaultSort
    {
        get
        {
            return defaultSort ?? (x => x.Id);
        }
        set => defaultSort = value;
    }

    public string PropertyName { get; set; } = propertyName;

    public bool IsAscending { get; set; } = isAscending;

    public virtual IQueryable<T> ApplySort(IQueryable<T> query, bool isAlreadySorted = false)
    {
        if (string.IsNullOrEmpty(PropertyName))
        {
            return isAlreadySorted
                ? (IsAscending ? ((IOrderedQueryable<T>)query).ThenBy(DefaultSort) : ((IOrderedQueryable<T>)query).ThenByDescending(DefaultSort))
                : (IsAscending ? query.OrderBy(DefaultSort) : query.OrderByDescending(DefaultSort));
        }

        var sortExpression = GetSortingExpression(PropertyName.ToLower());

        return isAlreadySorted
            ? (IsAscending ? ((IOrderedQueryable<T>)query).ThenBy(sortExpression) : ((IOrderedQueryable<T>)query).ThenByDescending(sortExpression))
            : (IsAscending ? query.OrderBy(sortExpression) : query.OrderByDescending(sortExpression));
    }

    protected abstract Expression<Func<T, object>> GetSortingExpression(string propertyName);
}