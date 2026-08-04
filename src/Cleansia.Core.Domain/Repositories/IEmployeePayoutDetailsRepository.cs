using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IEmployeePayoutDetailsRepository : IRepository<EmployeePayoutDetails, string>
{
    Task<EmployeePayoutDetails?> GetByEmployeeIdAsync(string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// The duplicate-destination signal (ADR-0034 D5.1/D6). Keyed on the derived IBAN, never the typed
    /// parts — <c>123456</c> and <c>0000123456</c> are the same account and comparing parts would
    /// silently under-report exactly the case worth seeing.
    /// </summary>
    Task<bool> IsIbanUsedByAnotherEmployeeAsync(string iban, string employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// GDPR erasure (ADR-0034 D1.1.2). Id-keyed, so it is correct regardless of what the caller loaded —
    /// <c>Employee.Anonymize()</c> reaching through the navigation would be a silent no-op on the real
    /// deletion query shape, and the account number would survive the erasure.
    /// </summary>
    Task RemoveForEmployeeAsync(string employeeId, CancellationToken cancellationToken);
}
