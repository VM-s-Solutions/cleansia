using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IUserRepository : IRepository<User, string>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailOrPhoneNumberAsync(string email, string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithConfirmationCodeAsync(string token, CancellationToken cancellationToken = default);
    Task<User?> GetByConfirmationCodeAsync(string token, CancellationToken cancellationToken = default);
    IQueryable<User> GetUnconfirmedUsersOlderThan(DateTime cutoffDate);
    Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
    IQueryable<User> GetConfirmedUsersWithEmails(IEnumerable<string> emails);
}