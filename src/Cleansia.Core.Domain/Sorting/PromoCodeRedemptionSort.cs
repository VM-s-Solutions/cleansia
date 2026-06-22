using System.Linq.Expressions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class PromoCodeRedemptionSort(string propertyName, bool isAscending)
    : BaseSort<PromoCodeRedemption>(propertyName, isAscending)
{
    public override Expression<Func<PromoCodeRedemption, object>> DefaultSort => x => x.RedeemedOn;

    protected override Expression<Func<PromoCodeRedemption, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(PromoCodeRedemption.RedeemedOn), StringComparison.CurrentCultureIgnoreCase))
            return x => x.RedeemedOn;
        if (string.Equals(propertyName, nameof(PromoCodeRedemption.AppliedDiscount), StringComparison.CurrentCultureIgnoreCase))
            return x => x.AppliedDiscount;
        return DefaultSort;
    }
}
