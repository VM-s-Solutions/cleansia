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

    /// <summary>
    /// Cross-tenant lookup by user id. Use only from system-level triggers
    /// (Stripe webhook, Azure Function) that have no tenant context. Caller
    /// MUST set <see cref="ITenantProvider.SetTenantOverride"/> with
    /// <c>user.TenantId</c> before mutating any tenant-scoped row.
    /// </summary>
    Task<User?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments the account's failed-login counter and opens the lockout window once
    /// <see cref="User.MaxFailedLoginAttempts"/> is reached. Persists immediately (the failing login
    /// command never commits the unit of work) and is a no-op while the account is already locked.
    /// </summary>
    Task RecordFailedLoginAsync(string email, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes one unit of the account's per-confirmation-code attempt budget.
    /// Returns false when the budget (<see cref="User.MaxCodeVerificationAttempts"/>) is spent.
    /// </summary>
    Task<bool> TryChargeConfirmationCodeAttemptAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes one unit of the account's per-reset-code attempt budget.
    /// Returns false when the budget (<see cref="User.MaxCodeVerificationAttempts"/>) is spent.
    /// </summary>
    Task<bool> TryChargeResetPasswordCodeAttemptAsync(string userId, CancellationToken cancellationToken = default);
}