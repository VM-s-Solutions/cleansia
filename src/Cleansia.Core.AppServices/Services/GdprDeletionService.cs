using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Mappers;
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
    ICartRepository cartRepository,
    IUserConsentRepository userConsentRepository,
    IGdprRequestRepository gdprRequestRepository,
    IDisputeRepository disputeRepository,
    ISavedAddressRepository savedAddressRepository,
    IOrderEmployeePayRepository orderEmployeePayRepository,
    IRecurringBookingTemplateRepository recurringBookingTemplateRepository,
    IUserNotificationRepository userNotificationRepository,
    IDeadLetterRepository deadLetterRepository,
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

    private async Task<bool> HasBlockingOrderAsync(string userId, CancellationToken cancellationToken)
    {
        var orders = await orderRepository.GetFiltered(o => o.UserId == userId)
            .Include(o => o.OrderStatusHistory)
            .ToListAsync(cancellationToken);

        return orders.Any(o =>
        {
            var status = o.GetCurrentOrderStatus();
            return status == OrderStatus.New
                || status == OrderStatus.Pending
                || status == OrderStatus.Confirmed
                || status == OrderStatus.InProgress;
        });
    }

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

        var devices = await deviceRepository.GetByUserIdAsync(user.Id, ct);
        deviceRepository.RemoveRange(devices);

        var notifications = await userNotificationRepository
            .GetFiltered(n => n.UserId == user.Id)
            .ToListAsync(ct);
        userNotificationRepository.RemoveRange(notifications);

        // Id-keyed for the same reason the payout row below is: a poisoned send-email body holds the
        // subject's address and real name verbatim, and its row has no navigation to a user at all.
        await deadLetterRepository.RemoveForSubjectAsync(user.Id, ct);

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
