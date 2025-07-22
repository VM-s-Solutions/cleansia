using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Infra.Database.Repositories;

public class OrderRepository(CleansiaDbContext context) : BaseRepository<Order>(context), IOrderRepository;