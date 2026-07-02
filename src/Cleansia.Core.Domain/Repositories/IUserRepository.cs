using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IUserRepository : IRepository<User, string>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// No-tracking variant of <see cref="GetByEmailAsync"/> for read-only profile surfaces
    /// (GetCurrentUser). Returns the SAME row + includes as the tracked variant; it
    /// just doesn't enrol the entity in the change tracker. The tracked variant stays the one shared
    /// with the mutation paths (Login/Register/ChangePassword), so do not flip it.
    /// </summary>
    Task<User?> GetByEmailNoTrackingAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// No-tracking variant of the base by-id read for read-only user surfaces (GetUser). Mirrors
    /// <c>GetQueryable</c>'s includes (PreferredLanguage) without tracking; the tracked base
    /// <c>GetByIdAsync</c> stays for load-then-mutate handlers.
    /// </summary>
    Task<User?> GetByIdNoTrackingAsync(string id, CancellationToken cancellationToken = default);
    Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailOrPhoneNumberAsync(string email, string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonymous-path variant of <see cref="GetByEmailAsync"/> for login / lockout / password-reset.
    /// Those requests carry no tenant claim, so the global tenant filter narrows every read to
    /// <c>TenantId == null</c> and a tenant-stamped account could never log in. Bypasses the filter;
    /// the caller-supplied email is the scope. Never use it on authenticated or registration
    /// surfaces — email uniqueness is per-tenant (the (TenantId, Email) unique index), so those must
    /// stay inside the filter.
    /// </summary>
    Task<User?> GetByEmailIgnoringTenantAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonymous-path variant of <see cref="ExistsWithEmailAsync"/>; same contract and constraints
    /// as <see cref="GetByEmailIgnoringTenantAsync"/>.
    /// </summary>
    Task<bool> ExistsWithEmailIgnoringTenantAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsWithConfirmationCodeAsync(string token, CancellationToken cancellationToken = default);
    Task<User?> GetByConfirmationCodeAsync(string token, CancellationToken cancellationToken = default);
    IQueryable<User> GetUnconfirmedUsersOlderThan(DateTime cutoffDate);
    Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);
    IQueryable<User> GetConfirmedUsersWithEmails(IEnumerable<string> emails);

    /// <summary>
    /// Cross-tenant lookup by user id. Use only from system-level triggers
    /// (Stripe webhook, Azure Function) and the anonymous refresh path, which
    /// have no tenant context. Caller MUST set
    /// <see cref="ITenantProvider.SetTenantOverride"/> with <c>user.TenantId</c>
    /// before mutating any tenant-scoped row.
    /// </summary>
    Task<User?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments the account's failed-login counter and opens the lockout window once
    /// <see cref="User.MaxFailedLoginAttempts"/> is reached. Persists immediately (the failing login
    /// command never commits the unit of work) and is a no-op while the account is already locked.
    /// </summary>
    Task RecordFailedLoginAsync(string email, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges one unit of the SAME lockout budget as <see cref="RecordFailedLoginAsync"/>, but keyed
    /// by user id, for a wrong current-password attempt on the authenticated change-password surface.
    /// Sharing the budget is deliberate: a change-password sprayer also bounds login. Atomic and a
    /// no-op while the account is already locked; persists immediately (the failing command never
    /// reaches the unit-of-work commit).
    /// </summary>
    Task RecordFailedCurrentPasswordAttemptAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken = default);

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