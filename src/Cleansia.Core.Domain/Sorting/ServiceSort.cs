using System.Linq.Expressions;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class ServiceSort(string propertyName, bool isAscending)
    : BaseSort<Service>(propertyName, isAscending)
{
    public override Expression<Func<Service, object>> DefaultSort => x => x.Name;

    protected override Expression<Func<Service, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Service.Name), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Name;
        if (string.Equals(propertyName, nameof(Service.EstimatedTime), StringComparison.CurrentCultureIgnoreCase))
            return x => x.EstimatedTime;
        // NO PRICE SORT. A catalogue entry has a price PER CURRENCY, so "cheapest first" has no answer
        // without naming one — and picking one silently is how a list quietly sorts by a market the
        // caller is not in. A price sort belongs on a query that carries a currency; until one does,
        // an unknown key falls through to DefaultSort exactly as every other unknown key already does.
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}