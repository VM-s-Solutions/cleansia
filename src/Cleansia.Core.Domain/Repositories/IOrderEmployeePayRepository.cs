using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.Domain.Repositories;

public interface IOrderEmployeePayRepository : IRepository<OrderEmployeePay, string>
{
    Task<OrderEmployeePay?> GetByOrderAndEmployeeAsync(string orderId, string employeeId, CancellationToken cancellationToken);
    Task<bool> ExistsWithOrderIdAndEmployeeIdAsync(string orderId, string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// All OrderEmployeePay rows for the given employee in the given pay
    /// period that have not yet been assigned to an EmployeeInvoice. Used
    /// by GenerateInvoice handler to materialize invoice line items, and
    /// by PayPeriodBackgroundService for the auto-invoice sweep. Includes
    /// Order for caller-side aggregation.
    /// </summary>
    Task<IReadOnlyList<OrderEmployeePay>> GetUnassignedForEmployeePeriodAsync(
        string employeeId, string payPeriodId, CancellationToken cancellationToken);

    /// <summary>
    /// Exists-check counterpart of <see cref="GetUnassignedForEmployeePeriodAsync"/>.
    /// Used by GenerateInvoice validator (refuse if there's nothing to invoice).
    /// </summary>
    Task<bool> HasUnassignedForEmployeePeriodAsync(
        string employeeId, string payPeriodId, CancellationToken cancellationToken);

    /// <summary>
    /// Sum of TotalPay across OrderEmployeePay rows owned by an employee
    /// that aren't yet attached to an invoice. Used by the dashboard
    /// "pending earnings" widget.
    /// </summary>
    Task<decimal> SumPendingEarningsAsync(string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// All OrderEmployeePay rows for an employee in a specific pay period
    /// (regardless of invoice assignment). Used by the period-pays summary
    /// widget.
    /// </summary>
    Task<IReadOnlyList<OrderEmployeePay>> GetByEmployeeAndPeriodAsync(
        string employeeId, string payPeriodId, CancellationToken cancellationToken);

    /// <summary>
    /// All OrderEmployeePay rows attached to a specific invoice. Used by
    /// invoice-PDF regeneration to render line items. Order is included.
    /// </summary>
    Task<IReadOnlyList<OrderEmployeePay>> GetByInvoiceIdAsync(
        string invoiceId, CancellationToken cancellationToken);

    /// <summary>
    /// All OrderEmployeePay rows belonging to an employee. Used by the
    /// GDPR deletion cascade to anonymize the employee's pay history.
    /// </summary>
    Task<IReadOnlyList<OrderEmployeePay>> GetByEmployeeIdAsync(
        string employeeId, CancellationToken cancellationToken);

    Task<bool> PayExistsForOrderAsync(string orderId, string employeeId, CancellationToken cancellationToken);
}
