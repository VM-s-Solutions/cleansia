using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.Orders;

public sealed class GuestOrderAccess(IOrderRepository orderRepository)
{
    public IQueryable<Order> OrdersForKey(IGuestOrderScopedRequest key)
    {
        var orders = orderRepository.GetQueryableIgnoringTenant();
        if (string.IsNullOrWhiteSpace(key.DisplayOrderNumber)
            || string.IsNullOrWhiteSpace(key.Email)
            || string.IsNullOrWhiteSpace(key.ConfirmationCode))
        {
            return orders.Where(o => false);
        }

        var email = key.Email.ToLowerInvariant();
        var code = key.ConfirmationCode.ToUpperInvariant();
        return orders.Where(o => o.UserId == null
            && o.DisplayOrderNumber == key.DisplayOrderNumber
            && o.CustomerEmail.ToLower() == email
            && o.ConfirmationCode.ToUpper() == code);
    }
}
