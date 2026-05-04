using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Loyalty;

/// <summary>
/// Paged activity ledger for the calling user's loyalty account. Joins
/// orders for <see cref="ActivityItem.OrderDisplayNumber"/> so the Rewards
/// activity row can render "+25 pts — Cleaning #ORD-A1B2C3D4".
/// </summary>
public class GetLoyaltyActivity
{
    public record Query(string UserId = "", int Offset = 0, int Limit = 20)
        : IRequest<PagedData<ActivityItem>>;

    public record ActivityItem(
        string Id,
        LoyaltyTransactionType Type,
        int Points,
        LoyaltyEarnSource Source,
        string? OrderId,
        string? OrderDisplayNumber,
        DateTimeOffset OccurredOn);

    internal class Handler(
        ILoyaltyAccountRepository loyaltyAccountRepository,
        ILoyaltyTransactionRepository loyaltyTransactionRepository,
        IOrderRepository orderRepository) : IRequestHandler<Query, PagedData<ActivityItem>>
    {
        public async Task<PagedData<ActivityItem>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pageNumber = request.Limit > 0 ? (request.Offset / request.Limit) + 1 : 1;

            var account = await loyaltyAccountRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (account == null)
            {
                return new PagedData<ActivityItem>(pageNumber, request.Limit, 0, Array.Empty<ActivityItem>());
            }

            var totalItems = await loyaltyTransactionRepository.CountForAccountAsync(account.Id, cancellationToken);
            var transactions = await loyaltyTransactionRepository.GetForAccountAsync(
                account.Id, request.Offset, request.Limit, cancellationToken);

            // Resolve OrderDisplayNumber for the page in one query.
            var orderIds = transactions
                .Where(t => !string.IsNullOrEmpty(t.OrderId))
                .Select(t => t.OrderId!)
                .Distinct()
                .ToList();

            var displayNumberLookup = orderIds.Count == 0
                ? new Dictionary<string, string>()
                : await orderRepository.GetQueryable()
                    .AsNoTracking()
                    .Where(o => orderIds.Contains(o.Id))
                    .Select(o => new { o.Id, o.DisplayOrderNumber })
                    .ToDictionaryAsync(x => x.Id, x => x.DisplayOrderNumber, cancellationToken);

            var items = transactions
                .Select(t => new ActivityItem(
                    Id: t.Id,
                    Type: t.Type,
                    Points: t.Points,
                    Source: t.Source,
                    OrderId: t.OrderId,
                    OrderDisplayNumber: t.OrderId != null && displayNumberLookup.TryGetValue(t.OrderId, out var d)
                        ? d
                        : null,
                    OccurredOn: t.OccurredOn))
                .ToList();

            return new PagedData<ActivityItem>(pageNumber, request.Limit, totalItems, items);
        }
    }
}
