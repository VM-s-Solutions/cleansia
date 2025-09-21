using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeeRepository(CleansiaDbContext context) : BaseRepository<Employee>(context), IEmployeeRepository
{
    public Task<Employee?> GetByUserEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.User != null && e.User.Email == email, cancellationToken);
    }

    public Task<bool> ExistsWithUserEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(e => e.User)
            .AnyAsync(e => e.User != null && e.User.Email == email, cancellationToken);
    }
}