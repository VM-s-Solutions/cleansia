using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class PayPeriodRepository(CleansiaDbContext context) : BaseRepository<PayPeriod>(context), IPayPeriodRepository
{
    public Task<PayPeriod?> GetPeriodForDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date, cancellationToken);
    }

    public Task<PayPeriod?> GetActivePeriodAsync(CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(p => p.Status == PayPeriodStatus.Open)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsActivePeriodAsync(CancellationToken cancellationToken)
    {
        return GetDbSet()
            .Where(p => p.Status == PayPeriodStatus.Open)
            .OrderByDescending(p => p.StartDate)
            .AnyAsync(cancellationToken);
    }

    public Task<bool> HasOverlappingPeriodAsync(DateOnly startDate, DateOnly endDate, string? excludeId, CancellationToken cancellationToken)
    {
        var query = GetDbSet()
            .Where(p => p.StartDate <= endDate && p.EndDate >= startDate);

        if (!string.IsNullOrWhiteSpace(excludeId))
        {
            query = query.Where(p => p.Id != excludeId);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<List<PayPeriod>> GetActivePeriodsEndingInDaysAsync(int daysFromNow, CancellationToken cancellationToken = default)
    {
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow));

        return GetDbSet()
            .Where(p => p.EndDate == targetDate && p.Status == PayPeriodStatus.Open)
            .ToListAsync(cancellationToken);
    }
}
