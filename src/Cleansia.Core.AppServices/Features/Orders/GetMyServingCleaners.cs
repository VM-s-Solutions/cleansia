using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Returns the cleaners who have COMPLETED at least one order for the
/// calling user. Drives the "request your favorite cleaner" picker on the
/// booking flow — Plus members pick one of these, the order's
/// <c>PreferredEmployeeId</c> is set, and the matching algorithm boosts
/// that cleaner's score.
///
/// Sorted by most-recent service date so the cleaner the customer worked
/// with most recently appears first. Capped at 20 — beyond that the picker
/// becomes useless and the customer is better served by the default
/// matching algorithm anyway.
/// </summary>
public class GetMyServingCleaners
{
    public record Query(string UserId = "") : ICommand<IReadOnlyList<Response>>;

    public record Response(
        string EmployeeId,
        string FullName,
        DateTime LastServedOn);

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(IOrderRepository orderRepository) : ICommandHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<BusinessResult<IReadOnlyList<Response>>> Handle(Query query, CancellationToken cancellationToken)
        {
            // Pull every Completed order for this user along with its
            // assigned employees + employee → user join for the display name.
            // Group by employee id, take the most-recent service date per
            // cleaner. Single round-trip; the AsSplitQuery hint keeps it
            // off the cartesian-explosion path.
            var rows = await orderRepository.GetQueryable()
                .Where(o => o.UserId == query.UserId
                    && o.OrderStatusHistory
                        .OrderByDescending(s => s.CreatedOn)
                        .Select(s => s.Status)
                        .FirstOrDefault() == OrderStatus.Completed)
                .Include(o => o.AssignedEmployees)
                    .ThenInclude(e => e.Employee)
                        .ThenInclude(emp => emp!.User)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var result = rows
                .SelectMany(o => o.AssignedEmployees.Select(e => new
                {
                    EmployeeId = e.EmployeeId,
                    FirstName = e.Employee?.User?.FirstName ?? string.Empty,
                    LastName = e.Employee?.User?.LastName ?? string.Empty,
                    ServedOn = o.CleaningDateTime,
                }))
                .Where(x => !string.IsNullOrEmpty(x.EmployeeId))
                .GroupBy(x => x.EmployeeId)
                .Select(g => new Response(
                    EmployeeId: g.Key,
                    FullName: $"{g.First().FirstName} {g.First().LastName}".Trim(),
                    LastServedOn: g.Max(x => x.ServedOn)))
                .OrderByDescending(r => r.LastServedOn)
                .Take(20)
                .ToList();

            return BusinessResult.Success<IReadOnlyList<Response>>(result);
        }
    }
}
