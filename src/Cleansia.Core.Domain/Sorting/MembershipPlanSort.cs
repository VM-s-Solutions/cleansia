using System.Linq.Expressions;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class MembershipPlanSort(string propertyName, bool isAscending)
    : BaseSort<MembershipPlan>(propertyName, isAscending)
{
    public override Expression<Func<MembershipPlan, object>> DefaultSort => x => x.BillingInterval;

    protected override Expression<Func<MembershipPlan, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(MembershipPlan.BillingInterval), StringComparison.CurrentCultureIgnoreCase))
            return x => x.BillingInterval;
        // NO PRICE SORT: a plan is priced per currency (MembershipPlanPrice), so the key falls through
        // to DefaultSort exactly as PackageSort's does, and for the same reason.
        if (string.Equals(propertyName, nameof(MembershipPlan.Code), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Code;
        if (string.Equals(propertyName, nameof(MembershipPlan.Name), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Name;
        if (string.Equals(propertyName, nameof(MembershipPlan.CreatedOn), StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}
