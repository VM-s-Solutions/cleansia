using System.Linq.Expressions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class PromoCodeSort(string propertyName, bool isAscending)
    : BaseSort<PromoCode>(propertyName, isAscending)
{
    public override Expression<Func<PromoCode, object>> DefaultSort => x => x.CreatedOn;

    protected override Expression<Func<PromoCode, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(PromoCode.Code), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Code;
        if (string.Equals(propertyName, nameof(PromoCode.IsActive), StringComparison.CurrentCultureIgnoreCase))
            return x => x.IsActive;
        if (string.Equals(propertyName, nameof(PromoCode.ValidUntil), StringComparison.CurrentCultureIgnoreCase))
            return x => x.ValidUntil ?? DateTimeOffset.MaxValue;
        if (string.Equals(propertyName, nameof(PromoCode.CurrentRedemptionsCount), StringComparison.CurrentCultureIgnoreCase))
            return x => x.CurrentRedemptionsCount;
        if (string.Equals(propertyName, nameof(PromoCode.CreatedOn), StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}
