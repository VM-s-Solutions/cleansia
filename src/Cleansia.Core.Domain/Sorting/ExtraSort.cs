using System.Linq.Expressions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class ExtraSort(string propertyName, bool isAscending)
    : BaseSort<Extra>(propertyName, isAscending)
{
    // The order the customer catalogue renders them in.
    public override Expression<Func<Extra, object>> DefaultSort => x => x.DisplayOrder;

    protected override Expression<Func<Extra, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Extra.Name), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Name;
        if (string.Equals(propertyName, nameof(Extra.DisplayOrder), StringComparison.CurrentCultureIgnoreCase))
            return x => x.DisplayOrder;
        // NO PRICE SORT -- a price per currency has no single order. See PackageSort.
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}
