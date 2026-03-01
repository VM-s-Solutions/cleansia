namespace Cleansia.Core.AppServices.Features.DataRetention;

public interface IDataRetentionBackgroundService
{
    Task RunAllRetentionTasksAsync(CancellationToken cancellationToken = default);
}
