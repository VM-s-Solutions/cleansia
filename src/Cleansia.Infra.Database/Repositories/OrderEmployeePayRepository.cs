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

    public Task<bool> ExistsWithOrderIdAndEmployeeIdAsync(string orderId, string employeeId, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(p => p.OrderId == orderId && p.EmployeeId == employeeId, cancellationToken);
    }

    public IQueryable<OrderEmployeePay> GetByEmployeeId(string employeeId)
    {
        return GetDbSet()
            .Include(p => p.Order)
            .Include(p => p.PayPeriod)
            .Include(p => p.EmployeeInvoice)
            .Where(p => p.EmployeeId == employeeId);
    }

    public IQueryable<OrderEmployeePay> GetByInvoiceId(string invoiceId)
    {
        return GetDbSet()
            .Include(p => p.Order)
            .Include(p => p.Employee)
            .Where(p => p.EmployeeInvoiceId == invoiceId);
    }

    public IQueryable<OrderEmployeePay> GetUnassignedPays(string employeeId)
    {
        return GetDbSet()
            .Include(p => p.Order)
            .Include(p => p.PayPeriod)
            .Where(p => p.EmployeeId == employeeId && p.EmployeeInvoiceId == null);
    }

    public Task<bool> PayExistsForOrderAsync(string orderId, string employeeId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .AnyAsync(p => p.OrderId == orderId && p.EmployeeId == employeeId, cancellationToken);
    }
}
