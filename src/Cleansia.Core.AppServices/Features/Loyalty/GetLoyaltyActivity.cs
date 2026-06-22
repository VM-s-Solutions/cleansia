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

namespace Cleansia.Core.AppServices.Features.Loyalty;

public class GetLoyaltyActivity
{
    public class Request : DataRangeRequest, IRequest<PagedData<ActivityItem>>;

    public record ActivityItem(
        LoyaltyTransactionType Type,
        int Points,
        LoyaltyEarnSource Source,
        string? OrderId,
        string? OrderDisplayNumber,
        DateTimeOffset OccurredOn);

    internal class Handler(
        ILoyaltyAccountRepository loyaltyAccountRepository,
        ILoyaltyTransactionRepository loyaltyTransactionRepository,
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider) : IRequestHandler<Request, PagedData<ActivityItem>>
    {
        public async Task<PagedData<ActivityItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;

            var account = await loyaltyAccountRepository.GetByUserIdAsync(userId, cancellationToken);
            if (account == null)
            {
                return Enumerable.Empty<ActivityItem>().MapToDto(0, request);
            }

            var specification = LoyaltyTransactionSpecification.Create(loyaltyAccountId: account.Id);
            var filter = specification.SatisfiedBy();

            var totalItems = await loyaltyTransactionRepository.GetCountAsync(filter, cancellationToken);
            var transactions = await loyaltyTransactionRepository
                .GetPagedSort<LoyaltyTransactionSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

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
                    Type: t.Type,
                    Points: t.Points,
                    Source: t.Source,
                    OrderId: t.OrderId,
                    OrderDisplayNumber: t.OrderId != null && displayNumberLookup.TryGetValue(t.OrderId, out var d)
                        ? d
                        : null,
                    OccurredOn: t.OccurredOn))
                .ToList();

            return items.MapToDto(totalItems, request);
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
