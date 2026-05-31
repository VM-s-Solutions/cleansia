namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IPayPeriodBackgroundService
{
    Task CloseExpiredPeriodsAndOpenNewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarantee there's an Open PayPeriod covering today. No-op if one
    /// already exists. Called inline before CalculateOrderPay so pay-calc
    /// never fails with "NoActivePeriod" just because the timer hasn't run
    /// yet or a fresh environment has none seeded.
    /// </summary>
    Task EnsureOpenPeriodAsync(CancellationToken cancellationToken = default);
}
