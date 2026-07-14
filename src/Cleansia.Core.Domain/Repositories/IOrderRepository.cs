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
    /// Lightweight count of orders an employee completed in the
    /// window [from, to). Filters directly off `Order.CompletedAt`
    /// and the employee's assignment — does NOT require a matching
    /// `OrderEmployeePay` row, so the dashboard "today / this week"
    /// counts reflect the cleaner's actual completion activity even
    /// before admin payroll has run for that order.
    /// </summary>
    Task<int> CountCompletedForEmployeeBetweenAsync(
        string employeeId, DateTime from, DateTime to, CancellationToken cancellationToken);

    /// <summary>
    /// The dashboard's four completion counts (this month / last month / today / this week) in ONE
    /// grouped query. Same semantics as four <see cref="CountCompletedForEmployeeBetweenAsync"/>
    /// calls with half-open [from, to) windows over <c>Order.CompletedAt</c>.
    /// </summary>
    Task<CompletedOrderWindowCounts> CountCompletedForEmployeeWindowsAsync(
        string employeeId,
        DateTime thisMonthStart, DateTime thisMonthEnd,
        DateTime lastMonthStart, DateTime lastMonthEnd,
        DateTime todayStart, DateTime todayEnd,
        DateTime weekStart, DateTime weekEnd,
        CancellationToken cancellationToken);

    /// <summary>
    /// Completed orders for an employee falling in EITHER of two inclusive [start, end] windows —
    /// one round trip covering the dashboard's week + last-month earnings windows (today is a subset
    /// of the week window; callers partition in memory). Loads the services/packages graph the pay
    /// estimator needs, same as <see cref="GetCompletedOrdersByDateRangeAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Order>> GetCompletedOrdersInEitherRangeAsync(
        string employeeId,
        DateTime firstStart, DateTime firstEnd,
        DateTime secondStart, DateTime secondEnd,
        CancellationToken cancellationToken);

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
    /// True when the employee is assigned to an order whose scheduled window
    /// ([CleaningDateTime, +EstimatedTime)) overlaps the given one AND whose current status is a
    /// live commitment (New, Pending, Confirmed, OnTheWay, InProgress). Terminal orders
    /// (Completed, Cancelled) no longer occupy the cleaner's time. Backs the TakeOrder
    /// time-conflict rule and the new-jobs digest's not-busy filter.
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

    /// <summary>
    /// Cross-tenant existence check by order id. The tenant-ignoring counterpart of
    /// <see cref="IRepository{Order,String}.ExistsAsync"/>, for the anonymous Stripe webhook validator:
    /// the request carries no tenant claim, so the tenant-scoped <c>ExistsAsync</c> collapses to
    /// <c>TenantId == null</c> and misses any non-null-tenant order — while the handler resolves it via
    /// <see cref="GetByIdIgnoringTenantAsync"/>. Mirror that read here so validator and handler agree.
    /// </summary>
    Task<bool> ExistsIgnoringTenantAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Cross-tenant lookup of the order owning a given Stripe payment intent.
    /// ONLY for Stripe webhook handlers that resolve an event carrying a
    /// <c>payment_intent</c> but no OrderId metadata (e.g. a <c>charge.dispute.*</c>
    /// chargeback). Same tenant-bypass contract as <see cref="GetByIdIgnoringTenantAsync"/>:
    /// the caller MUST call ITenantProvider.SetTenantOverride(order.TenantId) before any
    /// subsequent mutation so child rows inherit the right tenant.
    /// </summary>
    Task<Order?> GetByStripePaymentIntentIdIgnoringTenantAsync(string paymentIntentId, CancellationToken cancellationToken);

    /// <summary>
    /// Average + count of all OrderReview rows attached to orders that had
    /// this employee assigned. Used by the mobile dashboard's rating tile.
    /// Returns (null, 0) when the cleaner has never been reviewed.
    /// </summary>
    Task<(double? Average, int Count)> GetAverageRatingForEmployeeAsync(
        string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// ADR-0002 D3.4 + ADR-0004 C-B — receipt-side candidates for the dispatch
    /// reconciliation sweep: receipt-eligible orders (the receipt consumer's eligibility:
    /// <c>PaymentType == Cash</c> OR <c>PaymentStatus == Paid</c>) committed BEFORE
    /// <paramref name="olderThanUtc"/> whose receipt has not been fully realized — i.e.
    /// <c>Receipt is null</c> (the original D3.4 predicate) OR <c>Receipt.FiscalCode == null</c> (the
    /// ADR-0004 C-B widening that catches the claimed-but-unregistered rows the
    /// claim-before-register reorder creates).
    ///
    /// <para>System-job read — bypasses the tenant filter (<c>IgnoreQueryFilters</c>) so the timer can
    /// pick up orders across all tenants. The caller MUST set
    /// <c>ITenantProvider.SetTenantOverride(order.TenantId)</c> per item before any re-enqueue so the
    /// envelope carries the right tenant. The <c>enforcementMode != None</c> half of the C-B predicate
    /// is resolved per item in the sweep (it needs the per-country config), not in this SQL.</para>
    ///
    /// <para>Batch-bounded by <paramref name="take"/>; ordered oldest-first.</para>
    /// </summary>
    Task<List<Order>> GetReceiptReconciliationCandidatesAsync(
        DateTime olderThanUtc, int take, CancellationToken cancellationToken);
}