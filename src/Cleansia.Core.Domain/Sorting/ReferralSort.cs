using System.Linq.Expressions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class ReferralSort(string propertyName, bool isAscending)
    : BaseSort<Referral>(propertyName, isAscending)
{
    public override Expression<Func<Referral, object>> DefaultSort => x => x.AcceptedOn;

    protected override Expression<Func<Referral, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Referral.Status), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Status;
        if (string.Equals(propertyName, nameof(Referral.AcceptedOn), StringComparison.CurrentCultureIgnoreCase))
            return x => x.AcceptedOn;
        if (string.Equals(propertyName, nameof(Referral.FirstQualifyingOrderOn), StringComparison.CurrentCultureIgnoreCase))
            return x => x.FirstQualifyingOrderOn ?? DateTimeOffset.MaxValue;
        return DefaultSort;
    }
}
