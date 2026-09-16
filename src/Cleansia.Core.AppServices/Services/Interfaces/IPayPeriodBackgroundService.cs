using Cleansia.Core.Domain.EmployeePayroll;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IPayPeriodBackgroundService
{
    Task CloseExpiredPeriodsAndOpenNewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes one Open period of the ambient company, invoices and e-mails its cleaners, and — when
    /// asked — opens the next period after it. The nightly rollover asks; the company wind-down does
    /// not, because a company that has closed its door has no next period. The close is committed
    /// here, before anything is invoiced; the caller commits the successor.
    /// </summary>
    Task ClosePeriodAsync(PayPeriod period, string closeNote, bool openNext, CancellationToken cancellationToken);

    /// <summary>
    /// Guarantee there's an Open PayPeriod covering today. No-op if one
    /// already exists. Called inline before CalculateOrderPay so pay-calc
    /// never fails with "NoActivePeriod" just because the timer hasn't run
    /// yet or a fresh environment has none seeded.
    /// </summary>
    Task EnsureOpenPeriodAsync(CancellationToken cancellationToken = default);
}
