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

    public async Task<IReadOnlyList<EmployeeInvoice>> GetByEmployeeIdAsync(string employeeId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Where(i => i.EmployeeId == employeeId)
            .OrderByDescending(i => i.GeneratedAt)
            .ToListAsync(cancellationToken);
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

    public async Task<bool> AllInvoicesPaidInPeriodAsync(string payPeriodId, CancellationToken cancellationToken)
    {
        // Inverse: returns true iff zero unpaid invoices exist for the period.
        // Caller is ClosePayPeriod.Validator — period close is refused while
        // any invoice is still outstanding (Pending/Generated/Approved/etc).
        var hasUnpaid = await GetDbSet()
            .AnyAsync(i => i.PayPeriodId == payPeriodId && i.Status != EmployeeInvoiceStatus.Paid, cancellationToken);
        return !hasUnpaid;
    }

    public async Task<IReadOnlyList<EmployeeInvoice>> GetByEmployeeAndDateRangeAsync(
        string employeeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Include(i => i.OrderPays)
            .Where(i => i.EmployeeId == employeeId &&
                       i.GeneratedAt >= startDate &&
                       i.GeneratedAt <= endDate)
            .OrderBy(i => i.GeneratedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeInvoice>> GetAllByDateRangeAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(i => i.Employee)
                .ThenInclude(e => e.User)
            .Include(i => i.PayPeriod)
            .Include(i => i.Currency)
            .Where(i => i.GeneratedAt >= startDate &&
                       i.GeneratedAt <= endDate)
            .OrderBy(i => i.GeneratedAt)
            .ToListAsync(cancellationToken);
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
}
