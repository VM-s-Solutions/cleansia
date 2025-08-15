using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Orders;

public class OrderService : BaseEntity
{
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    public string ServiceId { get; private set; }
    public Service? Service { get; private set; }

    public static OrderService Create(Order order, Service service) => new()
    {
        Order = order,
        OrderId = order.Id,
        Service = service,
        ServiceId = service.Id
    };
}