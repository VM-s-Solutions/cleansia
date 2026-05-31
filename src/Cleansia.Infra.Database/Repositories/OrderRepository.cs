using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class OrderRepository(CleansiaDbContext context) : BaseRepository<Order>(context), IOrderRepository
{
    public async Task<IReadOnlyList<Order>> GetOrdersByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(x => x.CustomerPhone == phoneNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetEmployeeOrdersByDateRangeAsync(
        string employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.AssignedEmployees)
            .Include(o => o.SelectedServices)
                .ThenInclude(s => s.Service)
            .Include(o => o.SelectedPackages)
                .ThenInclude(op => op.Package)
            .Where(o => o.AssignedEmployees.Any(e => e.EmployeeId == employeeId) &&
                       o.CleaningDateTime >= startDate &&
                       o.CleaningDateTime <= endDate)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetCompletedOrdersByDateRangeAsync(
        string employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        // Filter directly off Order.CompletedAt (set inside the
        // domain at completion). Previous version filtered by
        // CleaningDateTime range AND inspected the last
        // OrderStatusHistory row for status=Completed — that meant
        // an order scheduled for May 20 but completed May 25 would
        // count as a "May 20 completion" in analytics, which is
        // wrong. Now "completed in [startDate, endDate]" means
        // exactly that.
        return await GetDbSet()
            .Include(o => o.AssignedEmployees)
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.SelectedServices)
                .ThenInclude(s => s.Service)
            .Include(o => o.SelectedPackages)
                .ThenInclude(op => op.Package)
            .Where(o => o.AssignedEmployees.Any(e => e.EmployeeId == employeeId) &&
                       o.CompletedAt >= startDate &&
                       o.CompletedAt <= endDate)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountCompletedForEmployeeBetweenAsync(
        string employeeId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        // Counts orders the cleaner actually completed in the
        // window. Half-open interval [from, to) matches the
        // dashboard caller's day/week math. No Includes —
        // pure COUNT(*) for the dashboard fast path.
        return GetDbSet()
            .Where(o => o.AssignedEmployees.Any(e => e.EmployeeId == employeeId)
                && o.CompletedAt >= from
                && o.CompletedAt < to)
            .CountAsync(cancellationToken);
    }


    public override Task<Order?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.Currency)
            .Include(o => o.SelectedServices)
                .ThenInclude(s => s.Service)
            .Include(o => o.SelectedPackages)
                .ThenInclude(op => op.Package)
                    .ThenInclude(p => p.IncludedServices)
                        .ThenInclude(s => s.Service)
            .Include(o => o.AssignedEmployees)
                .ThenInclude(ae => ae.Employee)
                    .ThenInclude(e => e.User)
            .Include(o => o.Receipt)
            .Include(o => o.CustomerAddress)
                .ThenInclude(ca => ca.Country)
            .Include(o => o.OrderNotes)
            .Include(o => o.OrderIssues)
            .Include(o => o.Reviews)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Order?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.Currency)
            .Include(o => o.SelectedServices)
                .ThenInclude(s => s.Service)
            .Include(o => o.SelectedPackages)
                .ThenInclude(op => op.Package)
                    .ThenInclude(p => p.IncludedServices)
                        .ThenInclude(s => s.Service)
            .Include(o => o.AssignedEmployees)
                .ThenInclude(ae => ae.Employee)
                    .ThenInclude(e => e.User)
            .Include(o => o.Receipt)
            .Include(o => o.CustomerAddress)
                .ThenInclude(ca => ca.Country)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetOrdersByDateRangeAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.SelectedServices)
                .ThenInclude(s => s.Service)
            .Include(o => o.SelectedPackages)
                .ThenInclude(op => op.Package)
            .Where(o => o.CleaningDateTime >= startDate &&
                       o.CleaningDateTime <= endDate)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetEmployeeOrderCountThisWeekAsync(string employeeId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var weekStart = today.AddDays(-daysSinceMonday);
        var weekEnd = weekStart.AddDays(7);

        return await GetDbSet()
            .Where(o => o.AssignedEmployees.Any(e => e.EmployeeId == employeeId) &&
                       o.CleaningDateTime >= weekStart &&
                       o.CleaningDateTime < weekEnd)
            .CountAsync(ct);
    }

    public async Task<bool> HasOverlappingOrderAsync(string employeeId, DateTime cleaningDateTime, int estimatedTimeMinutes, CancellationToken ct)
    {
        var newStart = cleaningDateTime;
        var newEnd = cleaningDateTime.AddMinutes(estimatedTimeMinutes);

        return await GetDbSet()
            .Where(o => o.AssignedEmployees.Any(e => e.EmployeeId == employeeId) &&
                       o.CleaningDateTime < newEnd &&
                       o.CleaningDateTime.AddMinutes(o.EstimatedTime) > newStart)
            .AnyAsync(ct);
    }

    public async Task<bool> UserHasCompletedOrderWithEmployeeAsync(string userId, string employeeId, CancellationToken ct)
    {
        // Most-recent status flip on each candidate order tells us if the
        // booking actually finished. Past Completed orders qualify; in-flight
        // ones don't (you can't request "the cleaner I'm currently with" as a
        // preference for a future booking — they need to have finished one).
        return await GetDbSet()
            .Where(o => o.UserId == userId
                && o.AssignedEmployees.Any(e => e.EmployeeId == employeeId)
                && o.OrderStatusHistory
                    .OrderByDescending(s => s.CreatedOn)
                    .Select(s => s.Status)
                    .FirstOrDefault() == OrderStatus.Completed)
            .AnyAsync(ct);
    }

    public async Task<(double? Average, int Count)> GetAverageRatingForEmployeeAsync(
        string employeeId, CancellationToken cancellationToken)
    {
        // Flatten reviews from orders this employee was assigned to. We
        // average across all of them; we don't filter by status because a
        // customer might review while the order is still "Completed-but-not-
        // closed" (existing system) and we want that signal too.
        var ratings = await GetDbSet()
            .Where(o => o.AssignedEmployees.Any(e => e.EmployeeId == employeeId))
            .SelectMany(o => o.Reviews)
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken);

        if (ratings.Count == 0) return (null, 0);
        return (ratings.Average(), ratings.Count);
    }
}