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
    /// time-conflict write gate.
    ///
    /// <para>TENANT-SCOPED, deliberately: a request path carries a <c>tenant_id</c> claim and the
    /// cleaner is in the caller's tenant. A caller with NO claim (timer, Function, webhook) must use
    /// <see cref="HasOverlappingOrderIgnoringTenantAsync"/> — under a tenant the global filter resolves
    /// <c>TenantId == null</c> against non-null rows, so this method finds nothing and reports the
    /// cleaner FREE, which is a guard saying yes.</para>
    ///
    /// <para>The scan is floored at <c>cleaningDateTime - Order.MaxOrderSpanHours</c>; see that
    /// constant for what the floor assumes.</para>
    /// </summary>
    Task<bool> HasOverlappingOrderAsync(string employeeId, DateTime cleaningDateTime, int estimatedTimeMinutes, CancellationToken ct);

    /// <summary>
    /// Same predicate as <see cref="HasOverlappingOrderAsync"/> with the tenant filter bypassed. For
    /// background sweeps that run with no tenant context and already select tenant-ignoring — today the
    /// new-jobs digest's not-busy filter, which would otherwise advertise clashing jobs to every
    /// tenanted cleaner.
    /// </summary>
    Task<bool> HasOverlappingOrderIgnoringTenantAsync(string employeeId, DateTime cleaningDateTime, int estimatedTimeMinutes, CancellationToken ct);

    /// <summary>
    /// ADR-0039 D3 — which of <paramref name="employeeIds"/> hold a live-commitment assignment
    /// overlapping <c>[windowStartUtc, windowEndUtc)</c>. Same window filter as
    /// <see cref="HasOverlappingOrderAsync"/>, in the shape a LIST of candidates needs.
    ///
    /// <para>Returns the BUSY subset, never the free one, so absence is the fail-OPEN default: a
    /// cleaner missing from the answer is treated as available, which is today's behaviour.</para>
    ///
    /// <para>ONE query for the whole set. Calling <see cref="HasOverlappingOrderAsync"/> in a loop over
    /// a candidate list on a request path is a hard reject — it is N unbounded scans per render, and it
    /// is what this method exists to replace.</para>
    ///
    /// <para>The preferred-cleaner picker and the hold resolver both call THIS method with the SAME
    /// window. Not the same rule — the same method: if the picker could say available and the resolver
    /// then say busy for a reason of its own, the feature has already failed.</para>
    ///
    /// <para>TENANT-SCOPED, deliberately, and there is no ignoring sibling: every caller is a request
    /// path with a claim (the recurring materializer runs under its own per-template tenant override).
    /// A background sweep asking about ONE cleaner already has
    /// <see cref="HasOverlappingOrderIgnoringTenantAsync"/>.</para>
    /// </summary>
    Task<IReadOnlySet<string>> GetBusyEmployeeIdsInWindowAsync(
        IReadOnlyCollection<string> employeeIds,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// ADR-0045 D1.1 — orders that still hold a LIVE preferred-cleaner reservation for
    /// <paramref name="employeeId"/> and overlap <c>[windowStartUtc, windowEndUtc)</c>. Read once, right
    /// after that cleaner commits to a job in the same window, so a reservation nobody can honour is
    /// ended at the moment it becomes unhonourable rather than left running for hours against a
    /// customer who was told someone was considering their booking.
    ///
    /// <para>A fourth TERMINAL SHAPE over the one window filter <see cref="HasOverlappingOrderAsync"/>
    /// and <see cref="GetBusyEmployeeIdsInWindowAsync"/> share — never a second overlap predicate,
    /// which ADR-0039 D3.2 rejects outright. It differs from its siblings in the other half only: they
    /// select on the ASSIGNMENT, this one on the RESERVATION.</para>
    ///
    /// <para>TENANT-SCOPED: the sole caller is <c>TakeOrder</c>, a request path with a claim.</para>
    /// </summary>
    Task<IReadOnlyList<Order>> GetLiveReservationsForBeneficiaryInWindowAsync(
        string employeeId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// True if the given user has previously had a Completed order assigned to
    /// the given employee. Used to validate <c>PreferredEmployeeId</c> on new
    /// bookings — customers can only request cleaners they've already worked
    /// with, preventing random employee-id probing.
    /// </summary>
    Task<bool> UserHasCompletedOrderWithEmployeeAsync(string userId, string employeeId, CancellationToken ct);

    /// <summary>
    /// The customer profile hero stats for <paramref name="userId"/> (T-0392):
    /// total bookings placed, total money saved (tier + promo + membership
    /// discounts summed over the user's non-cancelled orders), and the currency
    /// code of the user's most recent order. Returns
    /// <see cref="CustomerProfileStats.Empty"/> semantics for a user with no
    /// orders (zeros, null currency).
    /// </summary>
    Task<CustomerProfileStats> GetCustomerProfileStatsAsync(string userId, CancellationToken cancellationToken);

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
    /// ADR-0002 D3.4 + ADR-0004 C-B — receipt-eligible orders committed before
    /// <paramref name="olderThanUtc"/> whose receipt is missing OR carries no fiscal code. Batch-bounded
    /// by <paramref name="take"/>, oldest first.
    ///
    /// <para><b>System-job read — it bypasses the tenant filter, so the caller MUST set
    /// <c>ITenantProvider.SetTenantOverride(order.TenantId)</c> per item</b> before any re-enqueue, or
    /// the envelope carries the wrong tenant. → /flows/cross-cutting#tenancy</para>
    /// </summary>
    Task<List<Order>> GetReceiptReconciliationCandidatesAsync(
        DateTime olderThanUtc, int take, CancellationToken cancellationToken);
}