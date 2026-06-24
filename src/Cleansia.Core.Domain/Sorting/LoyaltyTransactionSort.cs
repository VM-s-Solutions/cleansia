using System.Linq.Expressions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class LoyaltyTransactionSort(string propertyName, bool isAscending)
    : BaseSort<LoyaltyTransaction>(propertyName, isAscending)
{
    public override Expression<Func<LoyaltyTransaction, object>> DefaultSort => x => x.OccurredOn;

    protected override Expression<Func<LoyaltyTransaction, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(LoyaltyTransaction.OccurredOn), StringComparison.CurrentCultureIgnoreCase))
            return x => x.OccurredOn;
        if (string.Equals(propertyName, nameof(LoyaltyTransaction.Points), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Points;
        if (string.Equals(propertyName, nameof(LoyaltyTransaction.Type), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Type;
        return DefaultSort;
    }
}
