using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class OrderRepository(CleansiaDbContext context) : BaseRepository<Order>(context), IOrderRepository
{
    public IQueryable<Order> GetOrdersByPhoneNumber(string phoneNumber)
    {
        return GetDbSet().Where(x => x.CustomerPhone == phoneNumber);
    }
}