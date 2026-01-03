using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeeInvoiceRepository(CleansiaDbContext context) : BaseRepository<EmployeeInvoice>(context), IEmployeeInvoiceRepository
{
    public Task<EmployeeInvoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(i => i.Employee)
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Include(i => i.OrderPays)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken);
    }

    public Task<EmployeeInvoice?> GetByVariableSymbolAsync(string variableSymbol, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(i => i.Employee)
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Include(i => i.OrderPays)
            .FirstOrDefaultAsync(i => i.VariableSymbol == variableSymbol, cancellationToken);
    }

    public IQueryable<EmployeeInvoice> GetByEmployeeId(string employeeId)
    {
        return GetDbSet()
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Where(i => i.EmployeeId == employeeId)
            .OrderByDescending(i => i.GeneratedAt);
    }

    public IQueryable<EmployeeInvoice> GetByStatus(EmployeeInvoiceStatus status)
    {
        return GetDbSet()
            .Include(i => i.Employee)
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Where(i => i.Status == status)
            .OrderBy(i => i.GeneratedAt);
    }

    public IQueryable<EmployeeInvoice> GetByPayPeriodId(string payPeriodId)
    {
        return GetDbSet()
            .Include(i => i.Employee)
            .Include(i => i.Currency)
            .Where(i => i.PayPeriodId == payPeriodId)
            .OrderBy(i => i.Employee.User!.LastName);
    }

    public Task<EmployeeInvoice?> GetByEmployeeAndPayPeriodAsync(string employeeId, string payPeriodId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(i => i.Employee)
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Include(i => i.OrderPays)
            .FirstOrDefaultAsync(i => i.EmployeeId == employeeId && i.PayPeriodId == payPeriodId, cancellationToken);
    }

    public Task<bool> ExistsForPayPeriodAsync(string employeeId, string payPeriodId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .AnyAsync(i => i.EmployeeId == employeeId && i.PayPeriodId == payPeriodId, cancellationToken);
    }

    public Task<EmployeeInvoice?> GetLatestInvoiceAsync(string employeeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(i => i.EmployeeId == employeeId)
            .OrderByDescending(i => i.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public IQueryable<EmployeeInvoice> GetInvoicesByDateRange(string employeeId, DateTime startDate, DateTime endDate)
    {
        return GetDbSet()
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Include(i => i.OrderPays)
            .Where(i => i.EmployeeId == employeeId &&
                       i.GeneratedAt >= startDate &&
                       i.GeneratedAt <= endDate)
            .OrderBy(i => i.GeneratedAt);
    }

    public override Task<EmployeeInvoice?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(i => i.Employee)
                .ThenInclude(e => e.User)
            .Include(i => i.PayPeriod)
            .Include(i => i.OrderPays)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public IQueryable<EmployeeInvoice> GetAllInvoicesByDateRange(DateTime startDate, DateTime endDate)
    {
        return GetDbSet()
            .Include(i => i.Employee)
                .ThenInclude(e => e.User)
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Where(i => i.GeneratedAt >= startDate &&
                       i.GeneratedAt <= endDate)
            .OrderBy(i => i.GeneratedAt);
    }
}
