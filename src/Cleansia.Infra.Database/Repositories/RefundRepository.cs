using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Cleansia.Infra.Database.Repositories;

public class RefundRepository(CleansiaDbContext context) : BaseRepository<Refund>(context), IRefundRepository
{
    public Task<Refund?> GetByRefundKeyAsync(string refundKey, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(r => r.RefundKey == refundKey, cancellationToken);
    }

    public Task<Refund?> GetSucceededByRefundKeyNoTrackingAsync(string refundKey, CancellationToken cancellationToken) =>
        GetDbSet().AsNoTracking().FirstOrDefaultAsync(r => r.RefundKey == refundKey && r.Status == RefundStatus.Succeeded, cancellationToken);

    public async Task<bool> HasPartialRefundAuditUnderLockAsync(string refundId, string refundKey, CancellationToken cancellationToken)
    {
        await Context.LockPartialRefundAuditAsync(refundId, cancellationToken);
        var json = JsonSerializer.Serialize(new { refundKey });
        if (Context.Database.IsNpgsql())
            return await Context.AdminActionAudits.AnyAsync(a => a.Action == "order.refund.partial" && a.Success
                && EF.Functions.JsonContains(a.AfterJson!, json), cancellationToken);
        var audits = await Context.AdminActionAudits.AsNoTracking()
            .Where(a => a.Action == "order.refund.partial" && a.Success && a.AfterJson != null).ToListAsync(cancellationToken);
        foreach (var audit in audits)
        {
            using var document = JsonDocument.Parse(audit.AfterJson!);
            if (document.RootElement.TryGetProperty("refundKey", out var key) && key.GetString() == refundKey) return true;
        }
        return false;
    }

    public async Task<decimal> GetSucceededRefundTotalForOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(r => r.OrderId == orderId && r.Status == RefundStatus.Succeeded)
            .SumAsync(r => r.Amount, cancellationToken);
    }

    public async Task<decimal> GetSucceededRefundTotalForOrderAsync(
        string orderId, string exceptRefundKey, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(r => r.OrderId == orderId
                && r.Status == RefundStatus.Succeeded
                && r.RefundKey != exceptRefundKey)
            .SumAsync(r => r.Amount, cancellationToken);
    }

    public async Task<decimal> GetPendingRefundTotalForOrderAsync(
        string orderId, string? exceptRefundKey, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(r => r.OrderId == orderId
                && r.Status == RefundStatus.Pending
                && (exceptRefundKey == null || r.RefundKey != exceptRefundKey))
            .SumAsync(r => r.Amount, cancellationToken);
    }

    public async Task<decimal> GetPendingRefundTotalClaimedBeforeAsync(Refund refund, CancellationToken cancellationToken)
    {
        var orderId = refund.OrderId;
        var claimedOn = refund.CreatedOn;
        return await GetDbSet()
            .Where(r => r.OrderId == orderId
                && r.Status == RefundStatus.Pending
                && r.CreatedOn < claimedOn)
            .SumAsync(r => r.Amount, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetSucceededRefundTotalsByOrderAsync(
        IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<string, decimal>(0);
        }

        var rows = await GetDbSet()
            .AsNoTracking()
            .Where(r => r.Status == RefundStatus.Succeeded && orderIds.Contains(r.OrderId))
            .GroupBy(r => r.OrderId)
            .Select(g => new { OrderId = g.Key, Total = g.Sum(r => r.Amount) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.OrderId, r => r.Total);
    }
}
