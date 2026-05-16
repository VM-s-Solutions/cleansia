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
}
