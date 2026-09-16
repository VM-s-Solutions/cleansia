using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Authentication;

public interface IOrderAccessService
{
    /// <summary>
    /// Strict access — admin, the order's customer, or an assigned employee.
    /// Use for sensitive paths (photos, receipts, mutations).
    /// </summary>
    Task<bool> CanAccessOrderAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// Loose access — same as <see cref="CanAccessOrderAsync"/> plus any
    /// employee can view an order that still has open spots (so cleaners
    /// can read the detail page before tapping Take). Use for read-detail
    /// paths only.
    /// </summary>
    Task<bool> CanBrowseOrderAsync(Order order, CancellationToken cancellationToken);

    bool IsCustomerCaller();

    Task<string?> GetCallerEmployeeIdAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Owner-pinned across companies for a customer; tenant-filtered for staff.
    /// Handlers must still apply the access or browse gate to the returned order.
    /// </summary>
    IQueryable<Order> OrdersForCaller();

    /// <summary>The detail graph of one order out of <see cref="OrdersForCaller"/>, or null.</summary>
    Task<Order?> LoadOrderForCallerAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>The validators' existence check over <see cref="OrdersForCaller"/>.</summary>
    Task<bool> OrderExistsForCallerAsync(string orderId, CancellationToken cancellationToken);
}
