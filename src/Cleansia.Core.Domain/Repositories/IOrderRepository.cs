using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Repositories;

public interface IOrderRepository : IRepository<Order, string>
{
    IQueryable<Order> GetOrdersByPhoneNumber(string phoneNumber);

    /// <summary>
    /// Gets orders for an employee within a date range.
    /// Used for analytics queries.
    /// </summary>
    IQueryable<Order> GetEmployeeOrdersByDateRange(string employeeId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets completed orders for an employee within a date range.
    /// Used for calculating earnings and time analytics.
    /// </summary>
    IQueryable<Order> GetCompletedOrdersByDateRange(string employeeId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets all orders within a date range.
    /// Used for admin revenue reports.
    /// </summary>
    IQueryable<Order> GetOrdersByDateRange(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Counts the number of orders assigned to an employee in the current week (Monday to Sunday).
    /// </summary>
    Task<int> GetEmployeeOrderCountThisWeekAsync(string employeeId, CancellationToken ct);

    /// <summary>
    /// Checks if an employee has an overlapping order at the given date/time range.
    /// </summary>
    Task<bool> HasOverlappingOrderAsync(string employeeId, DateTime cleaningDateTime, int estimatedTimeMinutes, CancellationToken ct);

    /// <summary>
    /// True if the given user has previously had a Completed order assigned to
    /// the given employee. Used to validate <c>PreferredEmployeeId</c> on new
    /// bookings — customers can only request cleaners they've already worked
    /// with, preventing random employee-id probing.
    /// </summary>
    Task<bool> UserHasCompletedOrderWithEmployeeAsync(string userId, string employeeId, CancellationToken ct);
}