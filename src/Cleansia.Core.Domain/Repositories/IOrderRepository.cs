using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Repositories;

public interface IOrderRepository : IRepository<Order, string>
{
    /// <summary>
    /// Orders matching the given phone number. Used by UpdateCurrentUser
    /// to back-fill the phone change onto the user's historical orders.
    /// </summary>
    Task<IReadOnlyList<Order>> GetOrdersByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Orders assigned to an employee whose CleaningDateTime falls in the
    /// given date range. Used for partner-side order analytics.
    /// </summary>
    Task<IReadOnlyList<Order>> GetEmployeeOrdersByDateRangeAsync(
        string employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);

    /// <summary>
    /// Completed orders for an employee within a date range. Used for
    /// calculating earnings + time analytics on the partner dashboard.
    /// </summary>
    Task<IReadOnlyList<Order>> GetCompletedOrdersByDateRangeAsync(
        string employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);

    /// <summary>
    /// All orders within a date range. Used by the admin revenue report.
    /// </summary>
    Task<IReadOnlyList<Order>> GetOrdersByDateRangeAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken);

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

    /// <summary>
    /// Cross-tenant lookup by order id. ONLY for use by Stripe webhook handlers
    /// and other system-level triggers that have no tenant context but need to
    /// resolve an order from a trusted external id (e.g. Stripe metadata).
    /// Caller MUST call ITenantProvider.SetTenantOverride(order.TenantId) before
    /// any subsequent mutation so child rows inherit the right tenant.
    /// </summary>
    Task<Order?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken);
}