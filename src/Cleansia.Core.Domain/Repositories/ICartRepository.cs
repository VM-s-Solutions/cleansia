using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface ICartRepository : IRepository<Cart, string>
{
    Task<Cart?> GetByUserEmailAsync(string email, CancellationToken cancellationToken);
}