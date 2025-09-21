using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserRepository(CleansiaDbContext context)
    : BaseRepository<User>(context), IUserRepository
{
    public override IQueryable<User> GetQueryable()
    {
        return GetDbSet()
            .Include(user => user.Orders)
            .AsQueryable();
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(user => user.Employee)
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        return GetDbSet().FirstOrDefaultAsync(user => user.PhoneNumber == phoneNumber, cancellationToken);
    }

    public Task<User?> GetByEmailOrPhoneNumberAsync(string email, string phoneNumber, CancellationToken cancellationToken = default)
    {
        return GetDbSet().FirstOrDefaultAsync(user => user.Email == email || user.PhoneNumber == phoneNumber, cancellationToken);
    }

    public Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet().AnyAsync(user => user.Email == email, cancellationToken);
    }

    public Task<bool> ExistsWithConfirmationCodeAsync(string token, CancellationToken cancellationToken = default)
    {
        return GetDbSet().AnyAsync(user => user.ConfirmationCode == token, cancellationToken);
    }

    public Task<User?> GetByConfirmationCodeAsync(string token, CancellationToken cancellationToken = default)
    {
        return GetDbSet().FirstOrDefaultAsync(user => user.ConfirmationCode == token, cancellationToken);
    }

    public IQueryable<User> GetUnconfirmedUsersOlderThan(DateTime cutoffDate)
    {
        return GetDbSet()
            .Where(user => !user.IsEmailConfirmed && user.CreatedOn <= cutoffDate)
            .AsQueryable();
    }

    public Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return GetQueryable().AnyAsync(user => user.PhoneNumber == phoneNumber, cancellationToken: cancellationToken);
    }

    public IQueryable<User> GetConfirmedUsersWithEmails(IEnumerable<string> emails)
    {
        return GetDbSet()
            .Where(user => user.IsEmailConfirmed)
            .Where(user => emails.Contains(user.Email));
    }
}
