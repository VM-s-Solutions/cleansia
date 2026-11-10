using System.Linq.Expressions;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class RefundSort(string propertyName, bool isAscending) : BaseSort<Refund>(propertyName, isAscending)
{
    public override Expression<Func<Refund, object>> DefaultSort => x => x.CreatedOn;

    protected override Expression<Func<Refund, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Refund.Amount), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Amount;
        }
        return DefaultSort;
    }
}
