using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeeInvoiceRepository : IRepository<EmployeeInvoice, string>
{
    Task<EmployeeInvoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken);

    Task<EmployeeInvoice?> GetByVariableSymbolAsync(string variableSymbol, CancellationToken cancellationToken);

    /// <summary>
    /// All invoices belonging to an employee, projected to read-only list.
    /// Used by the GDPR export to bundle the employee's invoice history.
    /// </summary>
    Task<IReadOnlyList<EmployeeInvoice>> GetByEmployeeIdAsync(string employeeId, CancellationToken cancellationToken);

    Task<EmployeeInvoice?> GetByEmployeeAndPayPeriodAsync(string employeeId, string payPeriodId, CancellationToken cancellationToken);

    Task<bool> ExistsForPayPeriodAsync(string employeeId, string payPeriodId, CancellationToken cancellationToken);

    Task<EmployeeInvoice?> GetLatestInvoiceAsync(string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// True iff every invoice in the given pay period is Paid. Used by the
    /// admin "close pay period" validator — closing is refused while any
    /// invoice is still outstanding.
    /// </summary>
    Task<bool> AllInvoicesPaidInPeriodAsync(string payPeriodId, CancellationToken cancellationToken);

    /// <summary>
    /// Invoices for an employee whose <c>GeneratedAt</c> falls in the given
    /// date range. Used by per-employee earnings analytics and the
    /// dashboard productivity widget.
    /// </summary>
    Task<IReadOnlyList<EmployeeInvoice>> GetByEmployeeAndDateRangeAsync(
        string employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);

    /// <summary>
    /// All invoices whose <c>GeneratedAt</c> falls in the given date range.
    /// Used by admin payroll report.
    /// </summary>
    Task<IReadOnlyList<EmployeeInvoice>> GetAllByDateRangeAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
}
