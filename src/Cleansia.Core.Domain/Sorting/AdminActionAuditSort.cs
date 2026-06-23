using System.Linq.Expressions;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class AdminActionAuditSort(string propertyName, bool isAscending)
    : BaseSort<AdminActionAudit>(propertyName, isAscending)
{
    public override Expression<Func<AdminActionAudit, object>> DefaultSort => x => x.OccurredOn;

    protected override Expression<Func<AdminActionAudit, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(AdminActionAudit.OccurredOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.OccurredOn;
        }
        if (string.Equals(propertyName, nameof(AdminActionAudit.ActorId), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.ActorId;
        }
        if (string.Equals(propertyName, nameof(AdminActionAudit.Action), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Action;
        }
        if (string.Equals(propertyName, nameof(AdminActionAudit.ResourceType), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.ResourceType ?? string.Empty;
        }
        if (string.Equals(propertyName, nameof(AdminActionAudit.Success), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Success;
        }
        return DefaultSort;
    }
}
