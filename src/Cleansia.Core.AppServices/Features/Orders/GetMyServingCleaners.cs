using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

public class GetMyServingCleaners
{
    public record Query : ICommand<IReadOnlyList<Response>>;

    public record Response(
        string EmployeeId,
        string FullName,
        DateTime LastServedOn);

    public class Handler(
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Query, IReadOnlyList<Response>>
    {
        public async Task<BusinessResult<IReadOnlyList<Response>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var rows = await orderRepository.GetQueryable()
                .Where(o => o.UserId == userId
                    && o.CurrentStatus == OrderStatus.Completed)
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
