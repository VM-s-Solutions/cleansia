using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Repositories;

public interface IOrderRepository : IRepository<Order, string>
{
    IQueryable<Order> GetOrdersByPhoneNumber(string phoneNumber);
}