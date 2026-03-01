using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class AdminDeleteUserAccount
{
    public record Command(string UserId) : ICommand;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(c => c.UserId)
                .NotEmpty()
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId);
        }
    }

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
            var adminEmail = userSessionProvider.GetUserEmail() ?? "admin";

            var hasPending = await gdprRequestRepository.HasPendingRequestAsync(request.UserId, "Deletion", cancellationToken);
            if (hasPending)
                return BusinessResult.Failure(new Error(
                    BusinessErrorMessage.GdprDeletionAlreadyPending, "A deletion request is already pending"));

            var user = await userRepository.GetQueryable()
                .Include(u => u.Employee).ThenInclude(e => e!.Address)
                .Include(u => u.Cart)
                .FirstAsync(u => u.Id == request.UserId, cancellationToken);

            var auditEntry = Core.Domain.Users.GdprRequest.Create(request.UserId, "Deletion");
            auditEntry.MarkProcessing();
            gdprRequestRepository.Add(auditEntry);

            // Delete profile photo blob
            if (!string.IsNullOrEmpty(user.ProfilePhotoName))
            {
                try
                {
                    var userFilesClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.UserFiles);
                    await userFilesClient.DeleteAsync(user.ProfilePhotoName, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete profile photo blob for user {UserId}", user.Id);
                }
            }

            // Delete employee documents blobs
            if (user.Employee is not null)
            {
                var documents = await employeeDocumentRepository.GetByEmployeeIdAsync(user.Employee.Id, true, cancellationToken);
                var docBlobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.EmployeeDocuments);
                foreach (var doc in documents)
                {
                    try
                    {
                        await docBlobClient.DeleteAsync(doc.FilePath, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to delete document blob {FilePath}", doc.FilePath);
                    }
                }
            }

            // Delete order photos blobs
            var customerOrderIds = await orderRepository.GetFiltered(o => o.UserId == request.UserId)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            var photoBlobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
            foreach (var orderId in customerOrderIds)
            {
                var photos = await orderPhotoRepository.GetPhotosByOrderIdAsync(orderId, cancellationToken);
                foreach (var photo in photos)
                {
                    try
                    {
                        var blobName = ExtractBlobNameFromUrl(photo.BlobUrl);
                        await photoBlobClient.DeleteAsync(blobName, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to delete order photo blob for order {OrderId}", orderId);
                    }
                }
            }

            // Anonymize customer orders
            var orders = await orderRepository.GetFiltered(o => o.UserId == request.UserId)
                .Include(o => o.CustomerAddress)
                .ToListAsync(cancellationToken);

            foreach (var order in orders)
            {
                order.AnonymizeCustomerData();
                order.CustomerAddress?.Anonymize();
            }

            // Hard delete devices
            var devices = await deviceRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            deviceRepository.RemoveRange(devices);

            // Hard delete cart
            if (user.Cart is not null)
                cartRepository.Remove(user.Cart);

            // Withdraw all consents
            var consents = await userConsentRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            foreach (var consent in consents.Where(c => c.IsGranted))
                consent.Withdraw();

            // Anonymize employee
            if (user.Employee is not null)
            {
                user.Employee.Anonymize();
                user.Employee.Address?.Anonymize();
                user.Employee.Deactivated("GDPR_ADMIN_DELETION", DateTimeOffset.UtcNow);
            }

            // Anonymize and deactivate user
            user.Anonymize();
            user.Deactivated("GDPR_ADMIN_DELETION", DateTimeOffset.UtcNow);

            auditEntry.MarkCompleted(adminEmail, $"Admin deletion by {adminEmail}");
            return BusinessResult.Success();
        }

        private static string ExtractBlobNameFromUrl(string blobUrl)
        {
            if (string.IsNullOrEmpty(blobUrl)) return blobUrl;
            var uri = new Uri(blobUrl);
            return uri.AbsolutePath.TrimStart('/');
        }
    }
}
