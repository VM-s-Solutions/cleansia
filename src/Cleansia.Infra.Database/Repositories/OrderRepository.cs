using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class OrderRepository(CleansiaDbContext context) : BaseRepository<Order>(context), IOrderRepository
{
    public IQueryable<Order> GetOrdersByPhoneNumber(string phoneNumber)
    {
        return GetDbSet().Where(x => x.CustomerPhone == phoneNumber);
    }

    public IQueryable<Order> GetEmployeeOrdersByDateRange(string employeeId, DateTime startDate, DateTime endDate)
    {
        return GetDbSet()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.AssignedEmployees)
            .Include(o => o.SelectedServices)
                .ThenInclude(s => s.Service)
            .Include(o => o.SelectedPackages)
                .ThenInclude(op => op.Package)
            .Where(o => o.AssignedEmployees.Any(e => e.EmployeeId == employeeId) &&
                       o.CleaningDateTime >= startDate &&
                       o.CleaningDateTime <= endDate)
            .AsSplitQuery();
    }

    public IQueryable<Order> GetCompletedOrdersByDateRange(string employeeId, DateTime startDate, DateTime endDate)
    {
        return GetDbSet()
            .Include(o => o.AssignedEmployees)
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.SelectedServices)
                .ThenInclude(s => s.Service)
            .Include(o => o.SelectedPackages)
                .ThenInclude(op => op.Package)
            .Where(o => o.AssignedEmployees.Any(e => e.EmployeeId == employeeId) &&
                       o.OrderStatusHistory.Any() &&
                       o.OrderStatusHistory.OrderByDescending(h => h.CreatedOn).First().Status == Cleansia.Core.Domain.Enums.OrderStatus.Completed &&
                       o.CleaningDateTime >= startDate &&
                       o.CleaningDateTime <= endDate)
            .AsSplitQuery();
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

    public IQueryable<Order> GetOrdersByDateRange(DateTime startDate, DateTime endDate)
    {
        return GetDbSet()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.SelectedServices)
                .ThenInclude(s => s.Service)
            .Include(o => o.SelectedPackages)
                .ThenInclude(op => op.Package)
            .Where(o => o.CleaningDateTime >= startDate &&
                       o.CleaningDateTime <= endDate)
            .AsSplitQuery();
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
}