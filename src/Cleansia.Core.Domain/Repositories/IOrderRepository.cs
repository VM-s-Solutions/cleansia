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
}