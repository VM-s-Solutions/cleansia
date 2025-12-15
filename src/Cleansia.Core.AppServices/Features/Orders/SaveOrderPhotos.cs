using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class SaveOrderPhotos
{
    public record Command(
        string OrderId,
        string EmployeeId,
        IEnumerable<PhotoToSave> Photos) : ICommand<Response>;

    public record PhotoToSave(
        PhotoType PhotoType,
        BlobFileDto File,
        string? Notes = null);

    public record Response(IEnumerable<SavedPhoto> Photos);

    public record SavedPhoto(
        string PhotoId,
        string BlobUrl,
        PhotoType PhotoType,
        DateTime CapturedAt);

    public class Validator : AbstractValidator<Command>
    {
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/jpg", "image/png", "image/webp" };

        public Validator(IOrderRepository orderRepository, IEmployeeRepository employeeRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x)
                .MustAsync(EmployeeIsAssignedToOrderAsync)
                .WithMessage(BusinessErrorMessage.EmployeeNotAssignedToOrder);

            RuleFor(x => x.Photos)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleForEach(x => x.Photos).ChildRules(photo =>
            {
                photo.RuleFor(p => p.File.FileName)
                    .NotEmpty()
                    .WithMessage(BusinessErrorMessage.Required)
                    .MaximumLength(255)
                    .WithMessage(BusinessErrorMessage.MaxLength);

                photo.RuleFor(p => p.File.Base64Content)
                    .NotEmpty()
                    .WithMessage(BusinessErrorMessage.FileRequired)
                    .Must(base64 => GetBase64DataSize(base64) <= MaxFileSizeBytes)
                    .WithMessage(BusinessErrorMessage.FileSizeExceeded);
            });
        }

        private static long GetBase64DataSize(string? base64)
        {
            if (string.IsNullOrEmpty(base64)) return 0;
            var data = base64.Contains(',') ? base64.Split(',')[1] : base64;
            return (long)(data.Length * 0.75); // Base64 is 4/3 of original size
        }

        private async Task<bool> EmployeeIsAssignedToOrderAsync(Command command, CancellationToken cancellationToken)
        {
            // Will be checked in handler
            return true;
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderPhotoRepository photoRepository,
        IBlobContainerClientFactory blobClientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Verify order exists and employee is assigned
            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            if (!order.AssignedEmployees.Any(oe => oe.EmployeeId == command.EmployeeId))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.EmployeeId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            // Upload all photos in batch
            var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
            var savedPhotos = new List<SavedPhoto>();

            foreach (var photoToSave in command.Photos)
            {
                var file = photoToSave.File;

                // Extract base64 data (remove data:image/xxx;base64, prefix if present)
                var base64Data = file.Base64Content!.Contains(',')
                    ? file.Base64Content.Split(',')[1]
                    : file.Base64Content;

                // Determine content type
                var contentType = DetermineContentType(file.FileName!, file.Base64Content);

                // Generate unique filename
                var fileExtension = Path.GetExtension(file.FileName);
                var uniqueFileName = $"{command.OrderId}_{photoToSave.PhotoType}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}{fileExtension}";
                var blobName = $"{DateTime.UtcNow.Year}/{command.OrderId}/{uniqueFileName}";

                // Upload to blob storage
                using var stream = new MemoryStream(Convert.FromBase64String(base64Data));
                var metadata = Metadata.CreateBuilder()
                    .WithMetadata(MetadataName.ContentType, contentType)
                    .Build();
                await blobClient.UploadAsync(blobName, stream, metadata, cancellationToken);

                var blobUrl = blobClient.GetBlobUri(blobName).ToString();

                // Create photo entity
                var photo = OrderPhoto.Create(
                    orderId: command.OrderId,
                    photoType: photoToSave.PhotoType,
                    blobUrl: blobUrl,
                    fileName: uniqueFileName,
                    originalFileName: file.FileName,
                    fileSizeBytes: stream.Length,
                    contentType: contentType,
                    capturedByEmployeeId: command.EmployeeId,
                    notes: photoToSave.Notes);

                photoRepository.Add(photo);

                savedPhotos.Add(new SavedPhoto(
                    PhotoId: photo.Id,
                    BlobUrl: blobUrl,
                    PhotoType: photoToSave.PhotoType,
                    CapturedAt: photo.CapturedAt));
            }

            return BusinessResult.Success(new Response(Photos: savedPhotos));
        }

        private static string DetermineContentType(string fileName, string? base64Content)
        {
            // Try to extract from base64 prefix
            if (!string.IsNullOrEmpty(base64Content) && base64Content.StartsWith("data:"))
            {
                var contentType = base64Content.Split(';')[0].Replace("data:", "");
                if (!string.IsNullOrEmpty(contentType))
                    return contentType;
            }

            // Fallback to file extension
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }
    }
}
