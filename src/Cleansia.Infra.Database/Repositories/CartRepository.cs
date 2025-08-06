using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class CartRepository(CleansiaDbContext context)
    : BaseRepository<Cart>(context), ICartRepository
{
    public Task<Cart?> GetByUserEmailAsync(string email, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(cart => cart.User)
            .Include(cart => cart.ServiceItems)
                .ThenInclude(item => item.Service)
            .Include(cart => cart.PackageItems)
                .ThenInclude(item => item.Package)
            .FirstOrDefaultAsync(cart => cart.User!.Email == email, cancellationToken);
    }

    public override IQueryable<Cart> GetQueryable()
    {
        return GetDbSet()
            .Include(cart => cart.User)
            .Include(cart => cart.ServiceItems)
            .Include(cart => cart.PackageItems)
            .AsQueryable();
    }
}