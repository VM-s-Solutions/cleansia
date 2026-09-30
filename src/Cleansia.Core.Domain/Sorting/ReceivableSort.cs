using System.Linq.Expressions;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class ReceivableSort(string propertyName, bool isAscending) : BaseSort<Receivable>(propertyName, isAscending)
{
    public override Expression<Func<Receivable, object>> DefaultSort => x => x.CreatedOn;

    protected override Expression<Func<Receivable, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Receivable.Amount), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Amount;
        }
        if (string.Equals(propertyName, nameof(Receivable.Status), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Status;
        }
        if (string.Equals(propertyName, nameof(Receivable.Kind), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Kind;
        }
        if (string.Equals(propertyName, nameof(Receivable.CreatedOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CreatedOn;
        }
        return DefaultSort;
    }
}
