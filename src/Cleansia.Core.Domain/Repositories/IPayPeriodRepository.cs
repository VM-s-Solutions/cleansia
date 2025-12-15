using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Repositories;

public interface IPayPeriodRepository : IRepository<PayPeriod, string>
{
    Task<PayPeriod?> GetPeriodForDateAsync(DateOnly date, CancellationToken cancellationToken);
    Task<PayPeriod?> GetActivePeriodAsync(CancellationToken cancellationToken);
    Task<bool> ExistsActivePeriodAsync(CancellationToken cancellationToken);
    IQueryable<PayPeriod> GetPeriodsByStatus(PayPeriodStatus status);

    IQueryable<PayPeriod> GetPeriodsForYear(int year);

    Task<bool> HasOverlappingPeriodAsync(DateOnly startDate, DateOnly endDate, string? excludeId, CancellationToken cancellationToken);

    Task<List<PayPeriod>> GetActivePeriodsEndingInDaysAsync(int daysFromNow, CancellationToken cancellationToken = default);
}
