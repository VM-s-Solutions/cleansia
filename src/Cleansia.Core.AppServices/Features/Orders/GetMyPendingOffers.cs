using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// ADR-0045 D9 — "jobs waiting for your answer". The cleaner-facing half of naming the reservation:
/// until now a held order appeared only on the ordinary available board, which is exactly why the perk
/// read as <i>priority</i> rather than <i>assignment</i>.
///
/// <para><b>No new predicate exists anywhere in this feature.</b> The surface is five existing
/// conjuncts and one equality, in the order the digest already uses: the beneficiary equality, the live
/// deadline, the seat arithmetic, the caller's own assignment, and <c>OrderAvailability.IsOfferableSql</c>
/// CONJOINED — never extended. <c>OrderAvailability</c> answers "is this live work someone may take", a
/// property of the order alone; "is it reserved for me right now" is the (order, cleaner) pair. Had this
/// needed a new arm in <c>OrderAvailability</c>, that would have been the signal the design was wrong.</para>
///
/// <para>The assignment conjunct is the one both siblings already carry — <c>NewJobsDigestService</c>'s
/// <c>AssignedEmployees.All(...)</c> and the available board's <c>excludeEmployeeId</c> — and it is what
/// keeps a multi-seat booking the caller has ALREADY taken off a list of things they have yet to answer.
/// <c>RequiredEmployees = ceil(EstimatedTime / 120)</c>, so every booking of four hours or more has a
/// second seat: the seat arithmetic still admits the order, ADR-0045 D1.1's implicit-decline sweep
/// deliberately spares the order just taken, and offerability is a property of the order alone. It is
/// "not me" and never "nobody", because a seat a rival took does not end the reservation on the seats
/// left.</para>
///
/// <para>The offerability conjunct is carried from day one, unlike <c>CanBrowseOrderAsync</c>: without
/// it a beneficiary would be shown a <c>New</c> + Card order whose money has not landed — one the take
/// gate refuses and <c>CleanupStalePendingOrders</c> may cancel within the hour. Under a DISCLOSED
/// reservation that reads as "you were assigned a job that vanished".</para>
///
/// <para>Bounded by construction rather than paged: a cleaner can hold only as many live reservations
/// as customers have named them inside one hold window, and the cap keeps a pathological row count off
/// a mobile surface.</para>
/// </summary>
public class GetMyPendingOffers
{
    private const int MaxOffers = 50;

    public record Query : IQuery<IReadOnlyList<PendingOfferItem>>;

    public class Handler(
        IOrderRepository orderRepository,
        IOrderAccessService orderAccessService) : IQueryHandler<Query, IReadOnlyList<PendingOfferItem>>
    {
        public async Task<BusinessResult<IReadOnlyList<PendingOfferItem>>> Handle(
            Query query, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);

            // A caller with no employee id is nobody's beneficiary. Answered without a query rather
            // than relying on `PreferredEmployeeId == null` matching, which C# and SQL disagree about.
            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Success<IReadOnlyList<PendingOfferItem>>([]);
            }

            var rows = await orderRepository
                .GetQueryable()
                .AsNoTracking()
                .Where(o => o.PreferredEmployeeId == employeeId)
                .Where(o => o.PreferredHoldUntilUtc > nowUtc)
                .Where(o => o.AssignedEmployees.Count < o.MaxEmployees)
                .Where(o => o.AssignedEmployees.All(ae => ae.EmployeeId != employeeId))
                .Where(OrderAvailability.IsOfferableSql)
                .OrderBy(o => o.PreferredHoldUntilUtc)
                .Take(MaxOffers)
                .Select(o => new PendingOfferRow(
                    o.Id,
                    o.DisplayOrderNumber,
                    o.CleaningDateTime,
                    o.EstimatedTime,
                    o.PreferredHoldUntilUtc!.Value,
                    o.CustomerAddress!.City,
                    o.CustomerAddress.ZipCode,
                    o.Rooms,
                    o.Bathrooms,
                    o.TotalPrice,
                    o.Currency.Code))
                .ToListAsync(cancellationToken);

            return BusinessResult.Success<IReadOnlyList<PendingOfferItem>>(
                [.. rows.Select(r => r.MapToDto())]);
        }
    }
}
