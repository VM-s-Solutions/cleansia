using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
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
    IUserNotificationRepository userNotificationRepository,
    ICustomerActionAuditRepository customerActionAuditRepository,
    IDisputeRepository disputeRepository,
    ITenantRepository tenantRepository,
    ITenantProvider tenantProvider,
    IAppConfigurationProvider configProvider,
    IDataRetentionConfig retentionConfig,
    IBlobContainerClientFactory blobClientFactory,
    ILogger<DataRetentionBackgroundService> logger)
    : IDataRetentionBackgroundService
{
    public async Task RunAllRetentionTasksAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Data retention job started");

        // The default lives in code, where an empty database cannot silence it. This gate used to read a
        // row from a FeatureFlags table that no migration ever inserted, and a missing row resolved to
        // "off" — so on every deployed database none of the tasks below had ever run once (T-0685). That
        // table is gone entirely (T-0689): once this switch left it, it gated nothing at all.
        if (!retentionConfig.Enabled)
        {
            logger.LogWarning("Data retention job disabled by configuration (DataRetention:Enabled). Skipping");
            return;
        }

        // Each operating company keeps its own windows (TenantSettingCatalog), so the job runs the tasks
        // once per company under its override: every read below is filtered to that company, its
        // settings are its own, and the commits inside each task stamp nothing else. No JWT on a job —
        // without the override a filtered read returns nothing at all.
        var tenantIds = await tenantRepository.GetAllIdsAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            tenantProvider.ClearTenantOverride();
            tenantProvider.SetTenantOverride(tenantId);

            await RunSafeAsync("ExpiredUserCodes", tenantId, CleanExpiredUserCodesAsync, cancellationToken);
            await RunSafeAsync("StaleDevices", tenantId, CleanStaleDevicesAsync, cancellationToken);
            await RunSafeAsync("OldGdprRequests", tenantId, CleanOldGdprRequestsAsync, cancellationToken);
            await RunSafeAsync("OrderCustomerPii", tenantId, CleanOrderCustomerPiiAsync, cancellationToken);
            await RunSafeAsync("WithdrawnConsents", tenantId, CleanWithdrawnConsentsAsync, cancellationToken);
            await RunSafeAsync("SupersededDocuments", tenantId, CleanSupersededDocumentsAsync, cancellationToken);
            await RunSafeAsync("UserNotifications", tenantId, CleanUserNotificationsAsync, cancellationToken);
            await RunSafeAsync("CustomerActionAudits", tenantId, CleanCustomerActionAuditsAsync, cancellationToken);
            await RunSafeAsync("DisputeText", tenantId, CleanExpiredDisputeTextAsync, cancellationToken);
        }

        tenantProvider.ClearTenantOverride();

        logger.LogInformation("Data retention job completed for {TenantCount} operating companies", tenantIds.Count);
    }

    private async Task RunSafeAsync(string taskName, string tenantId, Func<CancellationToken, Task> task, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Starting retention task: {Task} for {TenantId}", taskName, tenantId);
            await task(ct);
            logger.LogInformation("Completed retention task: {Task} for {TenantId}", taskName, tenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Retention task '{Task}' failed for {TenantId}", taskName, tenantId);
        }
    }

    private async Task CleanExpiredUserCodesAsync(CancellationToken ct)
    {
        if (!await configProvider.GetAsync(TenantSettingCatalog.ExpiredCodesEnabled, ct))
        {
            logger.LogInformation("ExpiredUserCodes task disabled by config");
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var confirmationCount = await userRepository.GetQueryable()
            .Where(u => u.ConfirmationCode != null && u.ConfirmationCodeExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.ConfirmationCode, (string?)null)
                .SetProperty(u => u.ConfirmationCodeExpiresAt, (DateTimeOffset?)null), ct);

        var resetCount = await userRepository.GetQueryable()
            .Where(u => u.ResetPasswordCode != null && u.ResetPasswordCodeExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.ResetPasswordCode, (string?)null)
                .SetProperty(u => u.ResetPasswordCodeExpiresAt, (DateTimeOffset?)null), ct);

        logger.LogInformation("Cleared {ConfirmCodes} confirmation codes and {ResetCodes} reset codes",
            confirmationCount, resetCount);
    }

    private async Task CleanStaleDevicesAsync(CancellationToken ct)
    {
        var days = await configProvider.GetAsync(TenantSettingCatalog.StaleDevicesDays, ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var totalDeleted = 0;

        while (true)
        {
            var batch = await deviceRepository.GetQueryable()
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
        var years = await configProvider.GetAsync(TenantSettingCatalog.GdprRequestsYears, ct);
        var cutoff = DateTimeOffset.UtcNow.AddYears(-years);

        var affected = await gdprRequestRepository.GetQueryable()
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
        var years = await configProvider.GetAsync(TenantSettingCatalog.OrderPiiYears, ct);
        var cutoff = DateTime.UtcNow.AddYears(-years);

        var totalProcessed = 0;

        while (true)
        {
            var batch = await orderRepository.GetQueryable()
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
        var years = await configProvider.GetAsync(TenantSettingCatalog.WithdrawnConsentsYears, ct);
        var cutoff = DateTimeOffset.UtcNow.AddYears(-years);

        var totalDeleted = 0;

        while (true)
        {
            var batch = await userConsentRepository.GetQueryable()
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
        var days = await configProvider.GetAsync(TenantSettingCatalog.DeletedDocumentsDays, ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.EmployeeDocuments);
        var totalDeleted = 0;

        while (true)
        {
            var batch = await employeeDocumentRepository.GetQueryable()
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

    private async Task CleanUserNotificationsAsync(CancellationToken ct)
    {
        var days = await configProvider.GetAsync(TenantSettingCatalog.NotificationsDays, ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        var totalDeleted = 0;

        while (true)
        {
            var batch = await userNotificationRepository.GetQueryable()
                .Where(n => n.CreatedOn < cutoff)
                .Take(RetentionDefaults.BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            userNotificationRepository.RemoveRange(batch);
            await userNotificationRepository.CommitAsync(ct);

            totalDeleted += batch.Count;
        }

        // Runaway cap: for any user beyond the newest MaxNotificationsPerUser rows, hard-delete
        // the overflow regardless of age (abuse guard, not a UX cap).
        var overCapUsers = await userNotificationRepository.GetQueryable()
            .GroupBy(n => n.UserId)
            .Where(g => g.Count() > RetentionDefaults.MaxNotificationsPerUser)
            .Select(g => g.Key)
            .ToListAsync(ct);

        foreach (var userId in overCapUsers)
        {
            var overflow = await userNotificationRepository.GetQueryable()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedOn)
                .Skip(RetentionDefaults.MaxNotificationsPerUser)
                .ToListAsync(ct);

            userNotificationRepository.RemoveRange(overflow);
            await userNotificationRepository.CommitAsync(ct);

            totalDeleted += overflow.Count;
        }

        logger.LogInformation(
            "Deleted {Total} user notifications (window: {Days} days, cap: {Cap}/user)",
            totalDeleted, days, RetentionDefaults.MaxNotificationsPerUser);
    }

    private async Task CleanCustomerActionAuditsAsync(CancellationToken ct)
    {
        // This is the one delete the append-only discipline sanctions, so a window of zero (cutoff =
        // now) or less (cutoff in the future) would empty the evidence table on the next tick; the
        // catalogue's floor of one is what keeps a misconfigured setting from becoming an instruction.
        var years = await configProvider.GetAsync(TenantSettingCatalog.CustomerAuditRetentionYears, ct);
        var cutoff = DateTimeOffset.UtcNow.AddYears(-years);

        // Per row by its own age, never anchored on the customer's last act: the anchor form kept an
        // active customer's IP addresses for the life of the account (ADR-0062 D5). The admin and
        // employee tables have no window (ADR-0012 D6) and this task must never reach them.
        var totalDeleted = await customerActionAuditRepository.DeleteExpiredAsync(cutoff, ct);

        logger.LogInformation("Deleted {Total} customer audit rows older than {Years} years",
            totalDeleted, years);
    }

    private async Task CleanExpiredDisputeTextAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var totalBlanked = 0;

        // The erasure stamped the window (GdprDeletionService reads the retention.dispute_text.years
        // setting); this task reads only the stamp. Anonymize() clears it, which is what makes each batch
        // shrink the backlog rather than re-read the same rows.
        while (true)
        {
            var batch = await disputeRepository.GetQueryable()
                .Where(d => d.TextRetainedUntil != null && d.TextRetainedUntil < now)
                .Include(d => d.Messages)
                .Include(d => d.Evidence)
                .Take(RetentionDefaults.BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            foreach (var dispute in batch)
            {
                dispute.Anonymize();
            }

            await disputeRepository.CommitAsync(ct);
            totalBlanked += batch.Count;
        }

        logger.LogInformation("Blanked the text of {Total} disputes whose retention window has passed", totalBlanked);
    }
}
