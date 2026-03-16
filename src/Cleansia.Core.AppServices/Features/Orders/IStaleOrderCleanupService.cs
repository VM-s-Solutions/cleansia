namespace Cleansia.Core.AppServices.Features.Orders;

public interface IStaleOrderCleanupService
{
    Task CancelStaleOrdersAsync(CancellationToken cancellationToken = default);
}
