using System.Linq.Expressions;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class DisputeSort(string propertyName, bool isAscending)
    : BaseSort<Dispute>(propertyName, isAscending)
{
    public override Expression<Func<Dispute, object>> DefaultSort => x => x.CreatedOn;

    protected override Expression<Func<Dispute, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Dispute.OrderId), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.OrderId;
        }
        if (string.Equals(propertyName, nameof(Dispute.UserId), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.UserId;
        }
        if (string.Equals(propertyName, nameof(Dispute.Status), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Status;
        }
        if (string.Equals(propertyName, nameof(Dispute.Reason), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Reason;
        }
        if (string.Equals(propertyName, nameof(Dispute.CreatedOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CreatedOn;
        }
        if (string.Equals(propertyName, nameof(Dispute.ResolvedOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.ResolvedOn ?? DateTimeOffset.MaxValue;
        }
        if (string.Equals(propertyName, nameof(Dispute.RefundAmount), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.RefundAmount ?? 0;
        }
        return DefaultSort;
    }
}
