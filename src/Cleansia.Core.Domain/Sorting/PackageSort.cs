using System.Linq.Expressions;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class PackageSort(string propertyName, bool isAscending)
    : BaseSort<Package>(propertyName, isAscending)
{
    public override Expression<Func<Package, object>> DefaultSort => x => x.Name;

    protected override Expression<Func<Package, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Package.Name), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Name;
        if (string.Equals(propertyName, nameof(Package.Price), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Price;
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}
