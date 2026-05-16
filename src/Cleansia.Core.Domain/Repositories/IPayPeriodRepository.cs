using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.Domain.Repositories;

public interface IPayPeriodRepository : IRepository<PayPeriod, string>
{
    Task<PayPeriod?> GetPeriodForDateAsync(DateOnly date, CancellationToken cancellationToken);
    Task<PayPeriod?> GetActivePeriodAsync(CancellationToken cancellationToken);
    Task<bool> ExistsActivePeriodAsync(CancellationToken cancellationToken);

    Task<bool> HasOverlappingPeriodAsync(DateOnly startDate, DateOnly endDate, string? excludeId, CancellationToken cancellationToken);

    Task<List<PayPeriod>> GetActivePeriodsEndingInDaysAsync(int daysFromNow, CancellationToken cancellationToken = default);
}
