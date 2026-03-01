using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class DeleteUserAccount
{
    public record Command : ICommand;

    internal class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IOrderRepository orderRepository,
        IEmployeeDocumentRepository employeeDocumentRepository,
        IOrderPhotoRepository orderPhotoRepository,
        IDeviceRepository deviceRepository,
        ICartRepository cartRepository,
        IUserConsentRepository userConsentRepository,
        IGdprRequestRepository gdprRequestRepository,
        IBlobContainerClientFactory blobClientFactory,
        ILogger<Handler> logger)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var email = userSessionProvider.GetUserEmail();
            var user = await userRepository.GetQueryable()
                .Include(u => u.Employee).ThenInclude(e => e!.Address)
                .Include(u => u.Cart)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            if (user is null)
                return BusinessResult.Failure(new Error(
                    BusinessErrorMessage.NotExistingUserWithEmail, "User not found"));

            var hasPending = await gdprRequestRepository.HasPendingRequestAsync(user.Id, "Deletion", cancellationToken);
            if (hasPending)
                return BusinessResult.Failure(new Error(
                    BusinessErrorMessage.GdprDeletionAlreadyPending, "A deletion request is already pending"));

            var auditEntry = Core.Domain.Users.GdprRequest.Create(user.Id, "Deletion");
            auditEntry.MarkProcessing();
            gdprRequestRepository.Add(auditEntry);

            await AnonymizeUserDataAsync(user, cancellationToken);

            auditEntry.MarkCompleted(email);
            return BusinessResult.Success();
        }

        private async Task AnonymizeUserDataAsync(Domain.Users.User user, CancellationToken ct)
        {
            // Delete profile photo blob
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

            // Delete employee documents blobs
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

            // Delete order photos blobs for orders where user is customer
            var customerOrders = await orderRepository.GetFiltered(o => o.UserId == user.Id)
                .Select(o => o.Id)
                .ToListAsync(ct);

            var photoBlobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
            foreach (var orderId in customerOrders)
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

            // Anonymize customer orders
            var orders = await orderRepository.GetFiltered(o => o.UserId == user.Id)
                .Include(o => o.CustomerAddress)
                .ToListAsync(ct);

            foreach (var order in orders)
            {
                order.AnonymizeCustomerData();
                order.CustomerAddress?.Anonymize();
            }

            // Hard delete devices (push tokens)
            var devices = await deviceRepository.GetByUserIdAsync(user.Id, ct);
            deviceRepository.RemoveRange(devices);

            // Hard delete cart
            if (user.Cart is not null)
                cartRepository.Remove(user.Cart);

            // Withdraw all consents
            var consents = await userConsentRepository.GetByUserIdAsync(user.Id, ct);
            foreach (var consent in consents.Where(c => c.IsGranted))
                consent.Withdraw();

            // Anonymize employee
            if (user.Employee is not null)
            {
                user.Employee.Anonymize();
                user.Employee.Address?.Anonymize();
                user.Employee.Deactivated("GDPR_DELETION", DateTimeOffset.UtcNow);
            }

            // Anonymize and deactivate user
            user.Anonymize();
            user.Deactivated("GDPR_DELETION", DateTimeOffset.UtcNow);
        }

        private static string ExtractBlobNameFromUrl(string blobUrl)
        {
            if (string.IsNullOrEmpty(blobUrl)) return blobUrl;
            var uri = new Uri(blobUrl);
            return uri.AbsolutePath.TrimStart('/');
        }
    }
}
