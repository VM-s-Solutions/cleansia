using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public class GdprDeletionService(
    IUserRepository userRepository,
    IOrderRepository orderRepository,
    IEmployeeDocumentRepository employeeDocumentRepository,
    IEmployeeInvoiceRepository employeeInvoiceRepository,
    IEmployeePayoutDetailsRepository employeePayoutDetailsRepository,
    IUserMembershipRepository userMembershipRepository,
    IOrderPhotoRepository orderPhotoRepository,
    IDeviceRepository deviceRepository,
    ILiveActivityTokenRepository liveActivityTokenRepository,
    ICartRepository cartRepository,
    IUserConsentRepository userConsentRepository,
    IGdprRequestRepository gdprRequestRepository,
    IDisputeRepository disputeRepository,
    ISavedAddressRepository savedAddressRepository,
    IOrderEmployeePayRepository orderEmployeePayRepository,
    IRecurringBookingTemplateRepository recurringBookingTemplateRepository,
    IUserNotificationRepository userNotificationRepository,
    IDeadLetterRepository deadLetterRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IRefreshTokenService refreshTokenService,
    IStripeClient stripeClient,
    IBlobContainerClientFactory blobClientFactory,
    ILogger<GdprDeletionService> logger)
    : IGdprDeletionService
{
    private const string DeletionRequestType = "Deletion";

    public async Task<BusinessResult> DeleteUserAccountAsync(
        string userId,
        string deactivationReason,
        Func<Domain.Users.User, (string ProcessedBy, string? Notes)> resolveAuditActor,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetQueryable()
            .Include(u => u.Employee).ThenInclude(e => e!.Address)
            .Include(u => u.Cart)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return BusinessResult.Failure(new Error(
                nameof(userId), BusinessErrorMessage.NotExistingUserWithEmail));

        var hasPending = await gdprRequestRepository.HasPendingRequestAsync(user.Id, DeletionRequestType, cancellationToken);
        if (hasPending)
            return BusinessResult.Failure(new Error(
                nameof(userId), BusinessErrorMessage.GdprDeletionAlreadyPending));

        var blockingOrder = await HasBlockingOrderAsync(user.Id, cancellationToken);
        if (blockingOrder)
            return BusinessResult.Failure(new Error(
                nameof(userId), BusinessErrorMessage.GdprDeletionBlockedByOrder));

        if (user.Employee is not null)
        {
            var blockingInvoice = await HasBlockingInvoiceAsync(user.Employee.Id, cancellationToken);
            if (blockingInvoice)
                return BusinessResult.Failure(new Error(
                    nameof(userId), BusinessErrorMessage.GdprDeletionBlockedByInvoice));
        }

        var auditEntry = Domain.Users.GdprRequest.Create(user.Id, DeletionRequestType);
        auditEntry.MarkProcessing();
        gdprRequestRepository.Add(auditEntry);

        await CancelActiveMembershipAsync(user.Id, cancellationToken);
        await AnonymizeUserDataAsync(user, deactivationReason, cancellationToken);

        var (processedBy, notes) = resolveAuditActor(user);
        auditEntry.MarkCompleted(processedBy, notes);
        return BusinessResult.Success();
    }

    /// <summary>
    /// Erasure is refused while the subject still has a LIVE order — every status except the two
    /// terminal ones. Deliberately the same membership as <c>OrderRepository.SlotBlockingStatuses</c>:
    /// ADR-0037 D5 already names the two as the pair of conservative-direction readers of one question.
    /// They stay two artifacts because they gate two different things in two layers — that one is the
    /// repository's slot-occupancy filter, this one is the erasure refusal — and ADR-0049 §D7 refuses
    /// promoting either to a shared platform grouping. <c>ErasureBlockingOrderStatusTests</c> pins them
    /// against each other instead, so the next divergence fails rather than sits.
    ///
    /// <para><c>OnTheWay</c> was missing here from the day the set was written, with no ticket, ADR or
    /// comment recording an intent to exclude it — while the DEAD <c>Pending</c> was present, which is
    /// the tell: no deliberate reading of "is this order live" admits a status nothing writes and
    /// refuses one a cleaner is actively driving under. Restored as the omission it was. Without it an
    /// erasure could complete while a cleaner was en route to the subject's home, anonymizing the
    /// customer record underneath a job that stays live and staffed.</para>
    /// </summary>
    private static readonly OrderStatus[] ErasureBlockingStatuses =
    [
        OrderStatus.New,
        OrderStatus.Pending,
        OrderStatus.Confirmed,
        OrderStatus.OnTheWay,
        OrderStatus.InProgress,
    ];

    private Task<bool> HasBlockingOrderAsync(string userId, CancellationToken cancellationToken)
        => orderRepository.GetFiltered(o => o.UserId == userId)
            .AnyAsync(o => ErasureBlockingStatuses.Contains(o.CurrentStatus), cancellationToken);

    private Task<bool> HasBlockingInvoiceAsync(string employeeId, CancellationToken cancellationToken)
    {
        return employeeInvoiceRepository.GetQueryable()
            .AnyAsync(i => i.EmployeeId == employeeId
                && (i.Status == EmployeeInvoiceStatus.Pending
                    || i.Status == EmployeeInvoiceStatus.Approved
                    || i.Status == EmployeeInvoiceStatus.Disputed),
                cancellationToken);
    }

    private async Task CancelActiveMembershipAsync(string userId, CancellationToken cancellationToken)
    {
        var membership = await userMembershipRepository.GetActiveForUserAsync(userId, cancellationToken);
        if (membership is null) return;

        try
        {
            await stripeClient.CancelSubscriptionAtPeriodEndAsync(
                membership.StripeSubscriptionId, cancellationToken);
            membership.MarkCancellationRequested();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Stripe subscription cancellation failed during GDPR delete for user {UserId}; manual reconciliation required",
                userId);
        }
    }

    private async Task AnonymizeUserDataAsync(Domain.Users.User user, string deactivationReason, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(user.ProfilePhotoName))
        {
            try
            {
                var userFilesClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.UserFiles);
                await userFilesClient.DeleteAsync(user.ProfilePhotoName, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete profile photo blob for user {UserId}", user.Id);
            }
        }

        if (user.Employee is not null)
        {
            var documents = await employeeDocumentRepository.GetByEmployeeIdAsync(user.Employee.Id, true, ct);
            var docBlobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.EmployeeDocuments);
            foreach (var doc in documents)
            {
                try
                {
                    await docBlobClient.DeleteAsync(doc.FilePath, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete document blob {FilePath} for user {UserId}", doc.FilePath, user.Id);
                }
            }

            // Ordered, not adjacent by accident, for the same reason the dispute-evidence pair below is:
            // FilePath is the only place the blob's name is stored, so the rows go after the blobs.
            await employeeDocumentRepository.RemoveForEmployeeAsync(user.Employee.Id, ct);
        }

        var customerOrderIds = await orderRepository.GetFiltered(o => o.UserId == user.Id)
            .Select(o => o.Id)
            .ToListAsync(ct);

        var photoBlobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
        foreach (var orderId in customerOrderIds)
        {
            var photos = await orderPhotoRepository.GetPhotosByOrderIdAsync(orderId, ct);
            foreach (var photo in photos)
            {
                try
                {
                    var blobName = ExtractBlobNameFromUrl(photo.BlobUrl);
                    await photoBlobClient.DeleteAsync(blobName, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete order photo blob for order {OrderId}", orderId);
                }

                // The row survives the blob because the order does — and it carries the uploader's own file
                // name and free-text note, which Order.AnonymizeCustomerData's review/note/issue walk never
                // reached (photos are not a navigation on the aggregate it loads).
                photo.Anonymize();
            }
        }

        var orders = await orderRepository.GetFiltered(o => o.UserId == user.Id)
            .Include(o => o.CustomerAddress)
            .Include(o => o.Reviews)
            .Include(o => o.OrderNotes)
            .Include(o => o.OrderIssues)
            .ToListAsync(ct);

        foreach (var order in orders)
        {
            order.AnonymizeCustomerData();
            order.CustomerAddress?.Anonymize();
        }

        // Every device row, not the active ones: logout soft-deletes a device and leaves the row present so
        // a later login can reclaim the tombstone, and the stale-device retention sweep filters on IsActive
        // too — so a logged-out handset's id and push token were reachable by neither path.
        await deviceRepository.RemoveForSubjectAsync(user.Id, ct);

        // The same handset's other APNs address. ADR-0029 D3 keeps activity registrations off the Device
        // aggregate, so nothing above reaches them: the sweeps that do (the 24h janitor, the logout/revoke
        // cascade) cover order-scoped rows and live sessions, and an erased account has neither — leaving
        // the per-install push-to-start row with no reaper at all.
        await liveActivityTokenRepository.RemoveForSubjectAsync(user.Id, ct);

        var notifications = await userNotificationRepository
            .GetFiltered(n => n.UserId == user.Id)
            .ToListAsync(ct);
        userNotificationRepository.RemoveRange(notifications);

        // Id-keyed for the same reason the payout row below is: a poisoned send-email body holds the
        // subject's address and real name verbatim, and its row has no navigation to a user at all.
        await deadLetterRepository.RemoveForSubjectAsync(user.Id, ct);

        // The same wire body one table over, before it poisons anything: an undrained or retry-exhausted
        // send-email outbox row holds the address and real name just as verbatim, and no prune reaches it.
        await outboxMessageRepository.RemoveForSubjectAsync(user.Id, ct);

        // Not a session cut — the refresh path already refuses a deactivated user — but a retention start.
        // RefreshTokenCleanupService hard-deletes only tokens that are revoked OR expired, so an untouched
        // live token keeps the subject's IP address, device label and device id until its own natural
        // expiry before the 90-day forensic window even begins. ADR-0027's directory is untouched: its poll
        // predicate reads the password_reset reason alone.
        await refreshTokenService.RevokeAllForUserAsync(
            user.Id, GdprAuditReasons.RefreshTokenRevocation, exceptRawToken: null, ct);

        if (user.Cart is not null)
            cartRepository.Remove(user.Cart);

        var consents = await userConsentRepository.GetByUserIdAsync(user.Id, ct);
        foreach (var consent in consents.Where(c => c.IsGranted))
            consent.Withdraw();

        var disputes = await disputeRepository.GetDisputesByUserIdAsync(user.Id, ct);
        var evidenceBlobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.DisputeEvidence);
        foreach (var dispute in disputes)
        {
            // These two steps are ordered, not adjacent by accident. Anonymize() overwrites
            // DisputeEvidence.FilePath, which is the only place the blob's name is stored — nothing else
            // in the database, the GDPR export or the audit log records it. Run it first and the delete
            // below is issued against "[DELETED]": the file survives with nothing left able to name it,
            // and the only way back is enumerating the container under the dispute id.
            foreach (var evidence in dispute.Evidence)
            {
                try
                {
                    await evidenceBlobClient.DeleteAsync(evidence.FilePath, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to delete dispute evidence blob {BlobName} on dispute {DisputeId} during erasure; the row's path is cleared next, so this name is the only remaining handle on the file",
                        evidence.FilePath, dispute.Id);
                }
            }

            dispute.Anonymize();
        }

        var savedAddresses = await savedAddressRepository.GetByUserAsync(user.Id, ct);
        savedAddressRepository.RemoveRange(savedAddresses);

        if (customerOrderIds.Count > 0)
        {
            var employeePays = await orderEmployeePayRepository
                .GetFiltered(p => customerOrderIds.Contains(p.OrderId))
                .ToListAsync(ct);
            foreach (var pay in employeePays)
                pay.Anonymize();
        }

        var templates = await recurringBookingTemplateRepository.GetByUserAsync(user.Id, ct);
        recurringBookingTemplateRepository.RemoveRange(templates);

        if (user.Employee is not null)
        {
            var employeeOwnedPays = await orderEmployeePayRepository
                .GetByEmployeeIdAsync(user.Employee.Id, ct);
            foreach (var pay in employeeOwnedPays)
                pay.Anonymize();

            // Id-keyed, not a navigation walk: this aggregate is loaded with only Employee → Address, and
            // there is no lazy loading, so a clear reaching through Employee.PayoutDetails would be a
            // silent no-op and the bank account, SWIFT and holder name would survive the erasure while
            // the request returned success (ADR-0034 D1.1.2). Anonymize() only drops the gate scalar.
            await employeePayoutDetailsRepository.RemoveForEmployeeAsync(user.Employee.Id, ct);

            user.Employee.Anonymize();
            user.Employee.Address?.Anonymize();
            user.Employee.Deactivated(deactivationReason, DateTimeOffset.UtcNow);
        }

        user.Anonymize();
        user.Deactivated(deactivationReason, DateTimeOffset.UtcNow);
    }

    private static string ExtractBlobNameFromUrl(string blobUrl)
    {
        if (string.IsNullOrEmpty(blobUrl)) return blobUrl;
        var uri = new Uri(blobUrl);
        return uri.AbsolutePath.TrimStart('/');
    }
}
