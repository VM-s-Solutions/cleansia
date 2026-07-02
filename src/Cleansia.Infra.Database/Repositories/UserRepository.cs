using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserRepository(CleansiaDbContext context)
    : BaseRepository<User>(context), IUserRepository
{
    public override IQueryable<User> GetQueryable()
    {
        // No blanket Include(Orders): every single-user fetch (GetUser, RefreshToken, ExportUserData,
        // admin user reads) was loading the user's entire order history, which no mapper reads. The
        // PreferredLanguage nav stays — the user DTOs render PreferredLanguage.Name. Callers that DO
        // need a nav add it explicitly (GdprDeletionService Includes Employee/Cart).
        return GetDbSet()
            .Include(user => user.PreferredLanguage)
            .AsQueryable();
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(user => user.Employee)
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByEmailNoTrackingAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .Include(user => user.Employee)
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByIdNoTrackingAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
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

    // Login / lockout / password-reset run on ANONYMOUS requests (no tenant claim), so the global
    // tenant filter narrows every read to TenantId == null and a tenant-stamped account could never
    // log in. IgnoreQueryFilters(); the caller-supplied email is the scope. Registration keeps the
    // filtered lookups — email uniqueness is per-tenant (the (TenantId, Email) unique index), so its
    // duplicate pre-check must stay tenant-scoped.
    public Task<User?> GetByEmailIgnoringTenantAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .IgnoreQueryFilters()
            .Include(user => user.Employee)
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    // Anonymous-path existence check; the comment above applies.
    public Task<bool> ExistsWithEmailIgnoringTenantAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Email == email, cancellationToken);
    }

    // The confirmation token is stored as a SHA-256 hash, so the incoming RAW
    // token is hashed and matched against the stored hash. Stays inside the global tenant filter
    // (no IgnoreQueryFilters) — a hashed token must not match cross-tenant.
    public Task<bool> ExistsWithConfirmationCodeAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = SecurityTokens.Hash(token);
        return GetDbSet().AnyAsync(user => user.ConfirmationCode == tokenHash, cancellationToken);
    }

    public Task<User?> GetByConfirmationCodeAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = SecurityTokens.Hash(token);
        return GetDbSet().FirstOrDefaultAsync(user => user.ConfirmationCode == tokenHash, cancellationToken);
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

    public Task<User?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetDbSet()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    // S7a — the lockout transition must be one atomic statement: a read-then-increment would let a
    // distributed guessing run race past the threshold. ExecuteUpdateAsync issues SQL outside the
    // UnitOfWork commit by design — the failing login command never commits, but the counter must
    // still land. Both SetProperty conditions evaluate against the PRE-update row (SQL semantics),
    // and the WHERE makes attempts during an open lockout a no-op so racing failures cannot extend it.
    // Hitting the threshold opens the window and resets the counter, so cooldown expiry restores a
    // fresh budget.
    //
    // IgnoreQueryFilters(): the failing login is anonymous (no tenant claim), so the filter would
    // narrow the WHERE to TenantId == null and a tenant-stamped account would never accrue failures
    // or lock — the email keys the scope, matching the login lookup.
    public async Task RecordFailedLoginAsync(string email, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var lockoutEnd = now.Add(User.FailedLoginLockout);

        await GetDbSet()
            .IgnoreQueryFilters()
            .Where(u => u.Email == email && (u.LockoutEndsAt == null || u.LockoutEndsAt <= now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    u => u.FailedLoginAttempts,
                    u => u.FailedLoginAttempts + 1 >= User.MaxFailedLoginAttempts ? 0 : u.FailedLoginAttempts + 1)
                .SetProperty(
                    u => u.LockoutEndsAt,
                    u => u.FailedLoginAttempts + 1 >= User.MaxFailedLoginAttempts ? lockoutEnd : u.LockoutEndsAt),
                cancellationToken);
    }

    // Same lockout transition as RecordFailedLoginAsync (S7a) but keyed by id for the authenticated
    // change-password surface, charging the SHARED FailedLoginAttempts/LockoutEndsAt pair so a
    // change-password sprayer also bounds login. Both SetProperty conditions evaluate against the
    // PRE-update row, and the WHERE makes attempts during an open lockout a no-op so racing failures
    // cannot extend it. Hitting the threshold opens the window and resets the counter, so cooldown
    // expiry restores a fresh budget.
    public async Task RecordFailedCurrentPasswordAttemptAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var lockoutEnd = now.Add(User.FailedLoginLockout);

        await GetDbSet()
            .Where(u => u.Id == userId && (u.LockoutEndsAt == null || u.LockoutEndsAt <= now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(
                    u => u.FailedLoginAttempts,
                    u => u.FailedLoginAttempts + 1 >= User.MaxFailedLoginAttempts ? 0 : u.FailedLoginAttempts + 1)
                .SetProperty(
                    u => u.LockoutEndsAt,
                    u => u.FailedLoginAttempts + 1 >= User.MaxFailedLoginAttempts ? lockoutEnd : u.LockoutEndsAt),
                cancellationToken);
    }

    public async Task<bool> TryChargeConfirmationCodeAttemptAsync(string userId, CancellationToken cancellationToken = default)
    {
        // S7a — atomic conditional increment; 0 rows affected = budget spent (no exception).
        // IgnoreQueryFilters(): the confirm flow is anonymous (no tenant claim), so the filter would
        // zero-match a tenant-stamped account and wrongly refuse its confirmation as budget-spent. The
        // id comes from the account the anonymous email lookup resolved (the OTP branch of
        // ConfirmUserEmail), so the scope never widens. Mirrors TryChargeResetPasswordCodeAttemptAsync.
        var rowsAffected = await GetDbSet()
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId && u.ConfirmationCodeAttempts < User.MaxCodeVerificationAttempts)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.ConfirmationCodeAttempts, u => u.ConfirmationCodeAttempts + 1),
                cancellationToken);

        return rowsAffected > 0;
    }

    public async Task<bool> TryChargeResetPasswordCodeAttemptAsync(string userId, CancellationToken cancellationToken = default)
    {
        // S7a — atomic conditional increment; 0 rows affected = budget spent (no exception).
        // IgnoreQueryFilters(): the reset flow is anonymous (no tenant claim), so the filter would
        // zero-match a tenant-stamped account and wrongly refuse its reset as budget-spent. The id
        // comes from the account the anonymous email lookup resolved, so the scope never widens.
        var rowsAffected = await GetDbSet()
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId && u.ResetPasswordCodeAttempts < User.MaxCodeVerificationAttempts)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.ResetPasswordCodeAttempts, u => u.ResetPasswordCodeAttempts + 1),
                cancellationToken);

        return rowsAffected > 0;
    }
}
