using System.Linq.Expressions;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Sorting;

public class GdprRequestSort(string propertyName, bool isAscending)
    : BaseSort<GdprRequest>(propertyName, isAscending)
{
    public override Expression<Func<GdprRequest, object>> DefaultSort => x => x.CreatedOn;

    protected override Expression<Func<GdprRequest, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(GdprRequest.CreatedOn), StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        if (string.Equals(propertyName, nameof(GdprRequest.Status), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Status;
        if (string.Equals(propertyName, nameof(GdprRequest.RequestType), StringComparison.CurrentCultureIgnoreCase))
            return x => x.RequestType;
        return DefaultSort;
    }
}
