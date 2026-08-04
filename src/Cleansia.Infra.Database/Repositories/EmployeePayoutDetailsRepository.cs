using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class EmployeePayoutDetailsRepository(CleansiaDbContext context)
    : BaseRepository<EmployeePayoutDetails>(context), IEmployeePayoutDetailsRepository
{
    public Task<EmployeePayoutDetails?> GetByEmployeeIdAsync(string employeeId, CancellationToken cancellationToken)
    {
        return GetDbSet().FirstOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken);
    }

    public Task<bool> IsIbanUsedByAnotherEmployeeAsync(string iban, string employeeId, CancellationToken cancellationToken)
    {
        return GetDbSet().AnyAsync(p => p.Iban == iban && p.EmployeeId != employeeId, cancellationToken);
    }

    public async Task RemoveForEmployeeAsync(string employeeId, CancellationToken cancellationToken)
    {
        // Loaded-and-removed rather than ExecuteDelete: the erasure runs inside the caller's unit of
        // work, and an ExecuteDelete would commit on its own connection — deleting the payout row even
        // when the rest of the erasure rolls back.
        var rows = await GetDbSet().Where(p => p.EmployeeId == employeeId).ToListAsync(cancellationToken);
        GetDbSet().RemoveRange(rows);
    }
}
