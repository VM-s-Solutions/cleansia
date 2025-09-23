using System.Linq.Expressions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class OrderSort(string propertyName, bool isAscending)
    : BaseSort<Order>(propertyName, isAscending)
{
    public override Expression<Func<Order, object>> DefaultSort => x => x.CreatedOn;

    protected override Expression<Func<Order, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Order.DisplayOrderNumber), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.DisplayOrderNumber;
        }
        if (string.Equals(propertyName, nameof(Order.CustomerName), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CustomerName;
        }
        if (string.Equals(propertyName, nameof(Order.CustomerEmail), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CustomerEmail;
        }
        if (string.Equals(propertyName, nameof(Order.CleaningDateTime), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CleaningDateTime;
        }
        if (string.Equals(propertyName, nameof(Order.TotalPrice), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.TotalPrice;
        }
        if (string.Equals(propertyName, nameof(Order.PaymentStatus), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.PaymentStatus;
        }
        if (string.Equals(propertyName, nameof(Order.PaymentType), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.PaymentType;
        }
        if (string.Equals(propertyName, nameof(Order.CreatedOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CreatedOn;
        }
        if (string.Equals(propertyName, nameof(Order.UpdatedOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.UpdatedOn;
        }
        return DefaultSort;
    }
}