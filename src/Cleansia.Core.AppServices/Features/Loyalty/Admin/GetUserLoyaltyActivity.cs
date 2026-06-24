using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SortDefinition = Cleansia.Core.Domain.Sorting.Common.SortDefinition;
using SortDirection = Cleansia.Core.Domain.Sorting.Common.SortDirection;

namespace Cleansia.Core.AppServices.Features.Loyalty.Admin;

/// <summary>
/// Admin paged ledger view for a specific user — the user-explicit mirror
/// of the customer's <see cref="Loyalty.GetLoyaltyActivity"/>. Resolves
/// order display numbers in one batch query for the page.
/// </summary>
public class GetUserLoyaltyActivity
{
    public class Request : DataRangeRequest, IRequest<PagedData<ActivityItem>>
    {
        public string UserId { get; init; } = default!;
    }

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
        IOrderRepository orderRepository) : IRequestHandler<Request, PagedData<ActivityItem>>
    {
        public async Task<PagedData<ActivityItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.UserId))
            {
                return Enumerable.Empty<ActivityItem>().MapToDto(0, request);
            }

            var account = await loyaltyAccountRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            if (account == null)
            {
                return Enumerable.Empty<ActivityItem>().MapToDto(0, request);
            }

            var specification = LoyaltyTransactionSpecification.Create(loyaltyAccountId: account.Id);
            var filter = specification.SatisfiedBy();

            var total = await loyaltyTransactionRepository.GetCountAsync(filter, cancellationToken);
            var txs = await loyaltyTransactionRepository
                .GetPagedSort<LoyaltyTransactionSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

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

            return items.MapToDto(total, request);
        }

        // Preserves the historical newest-first default: the bespoke repo ordered by
        // OccurredOn desc, and the empty-sort GetPagedSort path applies no ordering.
        private static IEnumerable<SortDefinition> ResolveSort(Request request)
        {
            var sort = request.Sort.MapToDomain().ToList();
            return sort.Count > 0
                ? sort
                : [new SortDefinition { Field = nameof(LoyaltyTransaction.OccurredOn), Direction = SortDirection.Descending }];
        }
    }
}
