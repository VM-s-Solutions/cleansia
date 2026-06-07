using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Loyalty.Admin;

/// <summary>
/// Admin paged ledger view for a specific user — the user-explicit mirror
/// of the customer's <see cref="Loyalty.GetLoyaltyActivity"/>. Resolves
/// order display numbers in one batch query for the page.
/// </summary>
public class GetUserLoyaltyActivity
{
    public record Query(string UserId, int Offset = 0, int Limit = 20)
        : IRequest<PagedData<ActivityItem>>;

    public record ActivityItem(
        string Id,
        LoyaltyTransactionType Type,
        int Points,
        LoyaltyEarnSource Source,
        string? OrderId,
        string? OrderDisplayNumber,
        string? Description,
        DateTimeOffset OccurredOn);

    internal class Handler(
        ILoyaltyAccountRepository loyaltyAccountRepository,
        ILoyaltyTransactionRepository loyaltyTransactionRepository,
        IOrderRepository orderRepository) : IRequestHandler<Query, PagedData<ActivityItem>>
    {
        public async Task<PagedData<ActivityItem>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pageNumber = request.Limit > 0 ? (request.Offset / request.Limit) + 1 : 1;

            if (string.IsNullOrEmpty(request.UserId))
            {
                return new PagedData<ActivityItem>(pageNumber, request.Limit, 0, Array.Empty<ActivityItem>());
            }

            var account = await loyaltyAccountRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (account == null)
            {
                return new PagedData<ActivityItem>(pageNumber, request.Limit, 0, Array.Empty<ActivityItem>());
            }

            var total = await loyaltyTransactionRepository.CountForAccountAsync(account.Id, cancellationToken);
            var txs = await loyaltyTransactionRepository.GetForAccountAsync(
                account.Id, request.Offset, request.Limit, cancellationToken);

            var orderIds = txs
                .Where(t => !string.IsNullOrEmpty(t.OrderId))
                .Select(t => t.OrderId!)
                .Distinct()
                .ToList();

            var displayLookup = orderIds.Count == 0
                ? new Dictionary<string, string>()
                : await orderRepository.GetQueryable()
                    .AsNoTracking()
                    .Where(o => orderIds.Contains(o.Id))
                    .Select(o => new { o.Id, o.DisplayOrderNumber })
                    .ToDictionaryAsync(x => x.Id, x => x.DisplayOrderNumber, cancellationToken);

            var items = txs
                .Select(t => new ActivityItem(
                    Id: t.Id,
                    Type: t.Type,
                    Points: t.Points,
                    Source: t.Source,
                    OrderId: t.OrderId,
                    OrderDisplayNumber: t.OrderId != null && displayLookup.TryGetValue(t.OrderId, out var d) ? d : null,
                    Description: t.Description,
                    OccurredOn: t.OccurredOn))
                .ToList();

            return new PagedData<ActivityItem>(pageNumber, request.Limit, total, items);
        }
    }
}
