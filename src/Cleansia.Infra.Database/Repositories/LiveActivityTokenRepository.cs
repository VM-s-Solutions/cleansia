using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class LiveActivityTokenRepository(CleansiaDbContext context)
    : BaseRepository<LiveActivityToken>(context), ILiveActivityTokenRepository
{
    public Task<LiveActivityToken?> GetByUserDeviceOrderAsync(string userId, string deviceId, string? orderId, CancellationToken cancellationToken)
    {
        return context.LiveActivityTokens
            .FirstOrDefaultAsync(
                t => t.UserId == userId && t.DeviceId == deviceId && t.OrderId == orderId,
                cancellationToken);
    }

    public Task<bool> HasTokensForOrderAsync(string userId, string orderId, CancellationToken cancellationToken)
    {
        return context.LiveActivityTokens
            .AnyAsync(
                t => t.UserId == userId && (t.OrderId == orderId || t.OrderId == null),
                cancellationToken);
    }

    public async Task<IReadOnlyList<LiveActivityToken>> GetByUserAndDeviceAsync(string userId, string deviceId, CancellationToken cancellationToken)
    {
        return await context.LiveActivityTokens
            .Where(t => t.UserId == userId && t.DeviceId == deviceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LiveActivityToken>> GetByUserAndOrderAsync(string userId, string orderId, CancellationToken cancellationToken)
    {
        return await context.LiveActivityTokens
            .Where(t => t.UserId == userId && t.OrderId == orderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LiveActivityToken>> GetPushToStartTokensAsync(string userId, CancellationToken cancellationToken)
    {
        return await context.LiveActivityTokens
            .Where(t => t.UserId == userId && t.OrderId == null)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveForSubjectAsync(string userId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters + the explicit subject predicate: registration is an authenticated mobile
        // route, so the row carries the SUBJECT's tenant, while an admin-driven erasure arrives with the
        // actor's — or with none at all. A tenant-scoped read would silently leave behind every row it
        // cannot see, and an erasure gets no second pass. A user id belongs to one person in any tenant, so
        // the predicate scopes rather than widens.
        var rows = await GetQueryableIgnoringTenant()
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);

        // Loaded-and-removed rather than ExecuteDelete: the erasure runs inside the caller's unit of work,
        // and an ExecuteDelete would commit on its own connection.
        GetDbSet().RemoveRange(rows);
    }

    public async Task<IReadOnlyList<LiveActivityToken>> GetStaleOrderScopedTokensAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        // Cross-tenant sweep (the timer holds no JWT) — bypass the tenant filter deliberately (S8).
        return await context.LiveActivityTokens
            .IgnoreQueryFilters()
            .Where(t => t.OrderId != null && t.LastUpdatedAt < cutoff)
            .ToListAsync(cancellationToken);
    }
}
