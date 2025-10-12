using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeeInvoiceRepository : IRepository<EmployeeInvoice, string>
{
    Task<EmployeeInvoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken);

    Task<EmployeeInvoice?> GetByVariableSymbolAsync(string variableSymbol, CancellationToken cancellationToken);

    IQueryable<EmployeeInvoice> GetByEmployeeId(string employeeId);

    IQueryable<EmployeeInvoice> GetByStatus(EmployeeInvoiceStatus status);

    IQueryable<EmployeeInvoice> GetByPayPeriodId(string payPeriodId);

    Task<EmployeeInvoice?> GetByEmployeeAndPayPeriodAsync(string employeeId, string payPeriodId, CancellationToken cancellationToken);

    Task<bool> ExistsForPayPeriodAsync(string employeeId, string payPeriodId, CancellationToken cancellationToken);
}
