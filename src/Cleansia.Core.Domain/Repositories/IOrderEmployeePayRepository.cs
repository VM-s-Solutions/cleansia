using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.Domain.Repositories;

public interface IOrderEmployeePayRepository : IRepository<OrderEmployeePay, string>
{
    Task<OrderEmployeePay?> GetByOrderAndEmployeeAsync(string orderId, string employeeId, CancellationToken cancellationToken);
    Task<bool> ExistsWithOrderIdAndEmployeeIdAsync(string orderId, string employeeId, CancellationToken cancellationToken);
    IQueryable<OrderEmployeePay> GetByEmployeeId(string employeeId);
    IQueryable<OrderEmployeePay> GetByInvoiceId(string invoiceId);
    IQueryable<OrderEmployeePay> GetUnassignedPays(string employeeId);
    Task<bool> PayExistsForOrderAsync(string orderId, string employeeId, CancellationToken cancellationToken);
}
