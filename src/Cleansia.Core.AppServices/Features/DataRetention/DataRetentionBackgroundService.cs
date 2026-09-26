using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Tenancy;
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
    IWorkContractAcceptanceRepository workContractAcceptanceRepository,
    IAddressRepository addressRepository,
    IOrderPhotoRepository orderPhotoRepository,
    IAdminActionAuditRepository adminActionAuditRepository,
    IEmployeeActionAuditRepository employeeActionAuditRepository,
    IGuestOrderAccessTokenRepository guestOrderAccessTokenRepository,
    ITenantRepository tenantRepository,
    ITenantProvider tenantProvider,
    IAppConfigurationProvider configProvider,
    IDataRetentionConfig retentionConfig,
    IBlobContainerClientFactory blobClientFactory,
    IArchiveWriteGate archiveWriteGate,
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

        // A company's GDPR obligations do not end with its trading: the windows keep blanking a
        // frozen company's books, which the archived-company write guard would otherwise refuse.
        using var legalObligation = archiveWriteGate.OpenForLegalObligation("data retention");

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
            await RunSafeAsync("WorkContractAcceptanceMetadata", tenantId, CleanWorkContractAcceptanceMetadataAsync, cancellationToken);
            await RunSafeAsync("OrderPhotos", tenantId, CleanOrderPhotosAsync, cancellationToken);
            await RunSafeAsync("AdminActionAudits", tenantId, CleanAdminActionAuditsAsync, cancellationToken);
            await RunSafeAsync("EmployeeActionAudits", tenantId, CleanEmployeeActionAuditsAsync, cancellationToken);
            await RunSafeAsync("GuestOrderAccessTokens", tenantId, CleanDeadGuestOrderAccessTokensAsync, cancellationToken);
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

            var sourceAddresses = batch.Where(o => o.CustomerAddress is not null)
                .Select(o => o.CustomerAddress!).DistinctBy(a => a.Id).ToList();

            // The customer is not erased here, so their own newer orders and saved address still count.
            var sharedAddressIds = await addressRepository.GetReferencedElsewhereAsync(
                sourceAddresses.Select(a => a.Id).ToList(),
                batch.Select(o => o.Id).ToList(),
                exceptSavedAddressIds: [],
                exceptEmployeeId: null,
                ct);

            foreach (var order in batch)
            {
                order.AnonymizeCustomerData();
                var addressCopy = order.AnonymizeCustomerAddress();
                if (addressCopy is not null)
                {
                    addressRepository.Add(addressCopy);
                }
            }

            // A new reference after the census makes the FK refuse deletion; it can never be blanked.
            addressRepository.RemoveRange(sourceAddresses.Where(a => !sharedAddressIds.Contains(a.Id)));
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
        // employee tables are swept by their own tasks, each under its own key.
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

    private async Task CleanWorkContractAcceptanceMetadataAsync(CancellationToken ct)
    {
        // Per row by its own age, as the customer audit window is (ADR-0062 D5): one row per job, so a
        // window anchored on the cleaner's last act would keep every IP for the life of the account.
        // Only the request trio goes; the acceptance itself is the contract record and is never deleted.
        var years = await configProvider.GetAsync(TenantSettingCatalog.WorkContractMetadataRetentionYears, ct);
        var cutoff = DateTimeOffset.UtcNow.AddYears(-years);

        var totalBlanked = await workContractAcceptanceRepository.PseudonymiseExpiredAsync(cutoff, RetentionDefaults.BatchSize, ct);

        logger.LogInformation("Blanked the request metadata on {Total} contract acceptances older than {Years} years",
            totalBlanked, years);
    }

    private async Task CleanOrderPhotosAsync(CancellationToken ct)
    {
        var days = await configProvider.GetAsync(TenantSettingCatalog.OrderPhotosDays, ct);
        var completedBefore = DateTime.UtcNow.AddDays(-days);
        var operatorTenantId = tenantProvider.GetCurrentTenantId()!;

        var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
        var totalDeleted = 0;
        string? afterId = null;

        // Paged by id rather than re-reading the head: a photo whose blob would not delete keeps its row,
        // since BlobUrl is the only name the blob has, and must not be read again in this run.
        while (true)
        {
            var batch = await orderPhotoRepository.GetPastRetentionAsync(
                operatorTenantId, completedBefore, afterId, RetentionDefaults.BatchSize, ct);

            if (batch.Count == 0) break;
            afterId = batch[^1].Id;

            foreach (var photo in batch)
            {
                try
                {
                    await blobClient.DeleteAsync(OrderPhotoBlobName.FromUrl(photo.BlobUrl), ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete the blob of order photo {PhotoId} on order {OrderId}, skipping",
                        photo.Id, photo.OrderId);
                    continue;
                }

                orderPhotoRepository.Remove(photo);
                totalDeleted++;
            }

            await orderPhotoRepository.CommitAsync(ct);
        }

        logger.LogInformation("Deleted {Total} order photos of orders completed more than {Days} days ago",
            totalDeleted, days);
    }

    private async Task CleanAdminActionAuditsAsync(CancellationToken ct)
    {
        var years = await configProvider.GetAsync(TenantSettingCatalog.AdminAuditRetentionYears, ct);
        var cutoff = DateTimeOffset.UtcNow.AddYears(-years);

        var totalDeleted = await adminActionAuditRepository.DeleteExpiredAsync(cutoff, ct);

        logger.LogInformation("Deleted {Total} admin audit rows older than {Years} years", totalDeleted, years);
    }

    private async Task CleanEmployeeActionAuditsAsync(CancellationToken ct)
    {
        var years = await configProvider.GetAsync(TenantSettingCatalog.EmployeeAuditRetentionYears, ct);
        var cutoff = DateTimeOffset.UtcNow.AddYears(-years);

        var totalDeleted = await employeeActionAuditRepository.DeleteExpiredAsync(cutoff, ct);

        logger.LogInformation("Deleted {Total} cleaner audit rows older than {Years} years", totalDeleted, years);
    }

    private async Task CleanDeadGuestOrderAccessTokensAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var totalDeleted = await guestOrderAccessTokenRepository.GetQueryable()
            .Where(t => t.RevokedOn != null || t.ExpiresOn <= now)
            .ExecuteDeleteAsync(ct);

        logger.LogInformation("Deleted {Total} expired or revoked guest order access tokens", totalDeleted);
    }
}
