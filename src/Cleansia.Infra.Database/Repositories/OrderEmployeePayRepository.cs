using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class OrderEmployeePayRepository(CleansiaDbContext context) : BaseRepository<OrderEmployeePay>(context), IOrderEmployeePayRepository
{
    public Task<OrderEmployeePay?> GetByOrderAndEmployeeAsync(string orderId, string employeeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Include(p => p.Order)
            .Include(p => p.Employee)
            .Include(p => p.PayPeriod)
            .Include(p => p.EmployeeInvoice)
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.EmployeeId == employeeId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetTotalPayByOrderIdsAsync(
        IReadOnlyCollection<string> orderIds, string employeeId, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<string, decimal>(0);
        }
        // Single SELECT, two columns, no Includes — replaces the
        // per-row loop in GetPagedOrders.
        var rows = await GetDbSet()
            .Where(p => p.EmployeeId == employeeId && orderIds.Contains(p.OrderId))
            .Select(p => new { p.OrderId, p.TotalPay })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.OrderId, r => r.TotalPay);
    }

    public Task<bool> ExistsWithOrderIdAndEmployeeIdAsync(string orderId, string employeeId, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(p => p.OrderId == orderId && p.EmployeeId == employeeId, cancellationToken);
    }

    public async Task<IReadOnlyList<OrderEmployeePay>> GetUnassignedForEmployeePeriodAsync(
        string employeeId, string payPeriodId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(p => p.Order)
            .Include(p => p.PayPeriod)
            .Where(p => p.EmployeeId == employeeId
                && p.PayPeriodId == payPeriodId
                && p.EmployeeInvoiceId == null)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasUnassignedForEmployeePeriodAsync(
        string employeeId, string payPeriodId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .AnyAsync(p => p.EmployeeId == employeeId
                && p.PayPeriodId == payPeriodId
                && p.EmployeeInvoiceId == null, cancellationToken);
    }

    public Task<decimal> SumPendingEarningsAsync(string employeeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(p => p.EmployeeId == employeeId && p.EmployeeInvoiceId == null)
            .SumAsync(p => p.TotalPay, cancellationToken);
    }

    public async Task<IReadOnlyList<OrderEmployeePay>> GetByEmployeeAndPeriodAsync(
        string employeeId, string payPeriodId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(p => p.Order)
            .Include(p => p.PayPeriod)
            .Where(p => p.EmployeeId == employeeId && p.PayPeriodId == payPeriodId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderEmployeePay>> GetByInvoiceIdAsync(
        string invoiceId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Include(p => p.Order)
            .Include(p => p.Employee)
            .Where(p => p.EmployeeInvoiceId == invoiceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderEmployeePay>> GetByEmployeeIdAsync(
        string employeeId, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(p => p.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> PayExistsForOrderAsync(string orderId, string employeeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .AnyAsync(p => p.OrderId == orderId && p.EmployeeId == employeeId, cancellationToken);
    }
}
