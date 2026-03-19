using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class LookupOrderBatch
{
    public record OrderLookupItem(string OrderId, string Email);

    public record Query(IEnumerable<OrderLookupItem> Items) : IQuery<Response>;

    public record Response(IEnumerable<LookupOrder.Response> Orders);

    public class Handler(IOrderRepository orderRepository) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var items = request.Items?.ToList() ?? [];
            if (items.Count == 0 || items.Count > 10)
                return BusinessResult.Success(new Response([]));

            var orderIds = items.Select(i => i.OrderId).Distinct().ToList();

            var orders = await orderRepository.GetQueryable()
                .Include(o => o.Currency)
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.SelectedServices)
                    .ThenInclude(s => s.Service)
                .Include(o => o.SelectedPackages)
                    .ThenInclude(op => op.Package)
                        .ThenInclude(p => p.IncludedServices)
                            .ThenInclude(s => s.Service)
                .AsSplitQuery()
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync(cancellationToken);

            // Only return orders where the email matches (security check)
            var lookupSet = items
                .Select(i => (i.OrderId, Email: i.Email.ToLower()))
                .ToHashSet();

            var matched = orders
                .Where(o => lookupSet.Contains((o.Id, o.CustomerEmail.ToLower())))
                .Select(o =>
                {
                    var detail = o.MapToDetail();
                    return new LookupOrder.Response(
                        detail.Id,
                        detail.DisplayOrderNumber,
                        detail.CustomerName,
                        detail.CleaningDateTime,
                        detail.PaymentType,
                        detail.PaymentStatus,
                        detail.TotalPrice,
                        detail.EstimatedTime,
                        detail.OrderStatus,
                        detail.ConfirmationCode,
                        detail.Currency,
                        detail.SelectedServices,
                        detail.SelectedPackages,
                        detail.StatusHistory,
                        detail.CreatedOn);
                })
                .ToList();

            return BusinessResult.Success(new Response(matched));
        }
    }
}
