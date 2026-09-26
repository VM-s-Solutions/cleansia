using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The remembered-bookings list a guest's browser refreshes in one call. Each item carries the same
/// per-order token <see cref="LookupOrder"/> takes; an item whose token matches nothing simply yields
/// no row, so the response never says which of the presented tokens was the wrong one.
/// </summary>
public class LookupOrderBatch
{
    public const int MaxItems = 10;

    public record Query(IEnumerable<string> AccessTokens) : IQuery<Response>;

    public record Response(IEnumerable<LookupOrder.Response> Orders);

    public class Handler(GuestOrderAccess guestOrderAccess) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var tokens = request.AccessTokens?
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct()
                .ToList() ?? [];
            if (tokens.Count == 0 || tokens.Count > MaxItems)
                return BusinessResult.Success(new Response([]));

            var orders = await guestOrderAccess.OrdersForTokens(tokens)
                .Include(o => o.Currency)
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.SelectedServices)
                    .ThenInclude(s => s.Service)
                .Include(o => o.SelectedPackages)
                    .ThenInclude(op => op.Package)
                        .ThenInclude(p => p.IncludedServices)
                            .ThenInclude(s => s.Service)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var matched = orders
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
                        o.ConfirmationCode,
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
