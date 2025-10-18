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
            .Include(e => e.Address)
            .Include(e => e.Nationality)
            .FirstOrDefaultAsync(e => e.User != null && e.User.Email == email, cancellationToken);
    }

    public Task<bool> ExistsWithUserEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(e => e.User)
            .AnyAsync(e => e.User != null && e.User.Email == email, cancellationToken);
    }

    public override Task<Employee?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(e => e.User)
            .Include(e => e.Address)
                .ThenInclude(a => a.Country)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }
}