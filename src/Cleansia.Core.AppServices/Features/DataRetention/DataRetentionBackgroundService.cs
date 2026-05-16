using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.DataRetention;

public class DataRetentionBackgroundService(
    IUserRepository userRepository,
    IDeviceRepository deviceRepository,
    IGdprRequestRepository gdprRequestRepository,
    IOrderRepository orderRepository,
    IUserConsentRepository userConsentRepository,
    IEmployeeDocumentRepository employeeDocumentRepository,
    IAppConfigurationProvider configProvider,
    IBlobContainerClientFactory blobClientFactory,
    ILogger<DataRetentionBackgroundService> logger)
    : IDataRetentionBackgroundService
{
    public async Task RunAllRetentionTasksAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Data retention job started");

        var isEnabled = await configProvider.IsFeatureEnabledAsync(
            RetentionDefaults.FeatureFlagName, cancellationToken: cancellationToken);

        if (!isEnabled)
        {
            logger.LogInformation("Data retention job disabled by feature flag. Skipping");
            return;
        }

        await RunSafeAsync("ExpiredUserCodes", CleanExpiredUserCodesAsync, cancellationToken);
        await RunSafeAsync("StaleDevices", CleanStaleDevicesAsync, cancellationToken);
        await RunSafeAsync("OldGdprRequests", CleanOldGdprRequestsAsync, cancellationToken);
        await RunSafeAsync("OrderCustomerPii", CleanOrderCustomerPiiAsync, cancellationToken);
        await RunSafeAsync("WithdrawnConsents", CleanWithdrawnConsentsAsync, cancellationToken);
        await RunSafeAsync("SupersededDocuments", CleanSupersededDocumentsAsync, cancellationToken);

        logger.LogInformation("Data retention job completed");
    }

    private async Task RunSafeAsync(string taskName, Func<CancellationToken, Task> task, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Starting retention task: {Task}", taskName);
            await task(ct);
            logger.LogInformation("Completed retention task: {Task}", taskName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Retention task '{Task}' failed", taskName);
        }
    }

    private async Task CleanExpiredUserCodesAsync(CancellationToken ct)
    {
        var setting = await configProvider.GetTenantSettingAsync(RetentionDefaults.ExpiredCodesEnabledKey, ct);
        if (setting?.Equals("false", StringComparison.OrdinalIgnoreCase) == true)
        {
            logger.LogInformation("ExpiredUserCodes task disabled by config");
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // System job — no JWT context. Use IgnoreQueryFilters so the sweep
        // sees rows across all tenants. Pure-modify (ExecuteUpdate) — no new
        // rows created, so no tenant override needed.
        var confirmationCount = await userRepository.GetQueryableIgnoringTenant()
            .Where(u => u.ConfirmationCode != null && u.ConfirmationCodeExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.ConfirmationCode, (string?)null)
                .SetProperty(u => u.ConfirmationCodeExpiresAt, (DateTimeOffset?)null), ct);

        var resetCount = await userRepository.GetQueryableIgnoringTenant()
            .Where(u => u.ResetPasswordCode != null && u.ResetPasswordCodeExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.ResetPasswordCode, (string?)null)
                .SetProperty(u => u.ResetPasswordCodeExpiresAt, (DateTimeOffset?)null), ct);

        logger.LogInformation("Cleared {ConfirmCodes} confirmation codes and {ResetCodes} reset codes",
            confirmationCount, resetCount);
    }

    private async Task CleanStaleDevicesAsync(CancellationToken ct)
    {
        var daysStr = await configProvider.GetTenantSettingAsync(RetentionDefaults.StaleDevicesDaysKey, ct);
        var days = int.TryParse(daysStr, out var d) ? d : RetentionDefaults.DefaultStaleDevicesDays;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var totalDeleted = 0;

        while (true)
        {
            var batch = await deviceRepository.GetQueryableIgnoringTenant()
                .Where(device => device.IsActive && device.LastActiveAt < cutoff)
                .Take(RetentionDefaults.BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            deviceRepository.RemoveRange(batch);
            await deviceRepository.CommitAsync(ct);

            totalDeleted += batch.Count;
        }

        logger.LogInformation("Deleted {Total} stale devices (cutoff: {Days} days)", totalDeleted, days);
    }

    private async Task CleanOldGdprRequestsAsync(CancellationToken ct)
    {
        var yearsStr = await configProvider.GetTenantSettingAsync(RetentionDefaults.GdprRequestsYearsKey, ct);
        var years = int.TryParse(yearsStr, out var y) ? y : RetentionDefaults.DefaultGdprRequestsYears;
        var cutoff = DateTimeOffset.UtcNow.AddYears(-years);

        var affected = await gdprRequestRepository.GetQueryableIgnoringTenant()
            .Where(r => r.Status == GdprRequestStatus.Completed
                     && r.CompletedAt < cutoff
                     && r.ProcessedBy != null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ProcessedBy, (string?)null), ct);

        logger.LogInformation("Anonymized ProcessedBy on {Count} GDPR requests older than {Years} years",
            affected, years);
    }

    private async Task CleanOrderCustomerPiiAsync(CancellationToken ct)
    {
        var yearsStr = await configProvider.GetTenantSettingAsync(RetentionDefaults.OrderPiiYearsKey, ct);
        var years = int.TryParse(yearsStr, out var y) ? y : RetentionDefaults.DefaultOrderPiiYears;
        var cutoff = DateTime.UtcNow.AddYears(-years);

        var totalProcessed = 0;

        while (true)
        {
            var batch = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.CleaningDateTime < cutoff
                         && o.CustomerName != AnonymizationMarker.Value
                         && o.OrderStatusHistory.Any(h => h.Status == OrderStatus.Completed))
                .Include(o => o.Reviews)
                .Include(o => o.OrderNotes)
                .Include(o => o.OrderIssues)
                .Include(o => o.CustomerAddress)
                .Take(RetentionDefaults.BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            foreach (var order in batch)
            {
                order.AnonymizeCustomerData();
                order.CustomerAddress?.Anonymize();
            }

            await orderRepository.CommitAsync(ct);
            totalProcessed += batch.Count;
        }

        logger.LogInformation("Anonymized PII on {Total} completed orders older than {Years} years",
            totalProcessed, years);
    }

    private async Task CleanWithdrawnConsentsAsync(CancellationToken ct)
    {
        var yearsStr = await configProvider.GetTenantSettingAsync(RetentionDefaults.WithdrawnConsentsYearsKey, ct);
        var years = int.TryParse(yearsStr, out var y) ? y : RetentionDefaults.DefaultWithdrawnConsentsYears;
        var cutoff = DateTimeOffset.UtcNow.AddYears(-years);

        var totalDeleted = 0;

        while (true)
        {
            var batch = await userConsentRepository.GetQueryableIgnoringTenant()
                .Where(c => !c.IsGranted && c.WithdrawnAt != null && c.WithdrawnAt < cutoff)
                .Take(RetentionDefaults.BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            userConsentRepository.RemoveRange(batch);
            await userConsentRepository.CommitAsync(ct);

            totalDeleted += batch.Count;
        }

        logger.LogInformation("Deleted {Total} withdrawn consents older than {Years} years",
            totalDeleted, years);
    }

    private async Task CleanSupersededDocumentsAsync(CancellationToken ct)
    {
        var daysStr = await configProvider.GetTenantSettingAsync(RetentionDefaults.DeletedDocumentsDaysKey, ct);
        var days = int.TryParse(daysStr, out var d) ? d : RetentionDefaults.DefaultDeletedDocumentsDays;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.EmployeeDocuments);
        var totalDeleted = 0;

        while (true)
        {
            var batch = await employeeDocumentRepository.GetQueryableIgnoringTenant()
                .Where(doc => !doc.IsActive && doc.DeactivatedOn < cutoff)
                .Take(RetentionDefaults.BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            foreach (var doc in batch)
            {
                try
                {
                    await blobClient.DeleteAsync(doc.FilePath, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete blob {FilePath} for document {DocId}, skipping",
                        doc.FilePath, doc.Id);
                    continue;
                }

                employeeDocumentRepository.Remove(doc);
            }

            await employeeDocumentRepository.CommitAsync(ct);
            totalDeleted += batch.Count;
        }

        logger.LogInformation("Purged {Total} superseded documents older than {Days} days",
            totalDeleted, days);
    }
}
