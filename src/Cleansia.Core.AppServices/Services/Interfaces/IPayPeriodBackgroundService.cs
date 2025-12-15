namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IPayPeriodBackgroundService
{
    Task CloseExpiredPeriodsAndOpenNewAsync(CancellationToken cancellationToken = default);
}
