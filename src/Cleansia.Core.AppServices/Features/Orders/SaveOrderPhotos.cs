using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
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
        private const long MaxFileSizeBytes = 10 * 1024 * 1024;

        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

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
            return (long)(data.Length * 0.75);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderPhotoRepository photoRepository,
        IOrderAccessService orderAccessService,
        IBlobContainerClientFactory blobClientFactory) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employeeId = await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.OrderId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            var order = await orderRepository
                .GetQueryable()
                .Include(o => o.AssignedEmployees)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            if (order == null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            if (!order.AssignedEmployees.Any(oe => oe.EmployeeId == employeeId))
            {
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), BusinessErrorMessage.EmployeeNotAssignedToOrder));
            }

            var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
            var savedPhotos = new List<SavedPhoto>();

            foreach (var photoToSave in command.Photos)
            {
                var file = photoToSave.File;

                var base64Data = file.Base64Content!.Contains(',')
                    ? file.Base64Content.Split(',')[1]
                    : file.Base64Content;

                var contentType = DetermineContentType(file.FileName!, file.Base64Content);

                var fileExtension = Path.GetExtension(file.FileName);
                var uniqueFileName = $"{command.OrderId}_{photoToSave.PhotoType}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}{fileExtension}";
                var blobName = $"{DateTime.UtcNow.Year}/{command.OrderId}/{uniqueFileName}";

                using var stream = new MemoryStream(Convert.FromBase64String(base64Data));
                var metadata = Metadata.CreateBuilder()
                    .WithMetadata(MetadataName.ContentType, contentType)
                    .Build();
                await blobClient.UploadAsync(blobName, stream, metadata, cancellationToken);

                var blobUrl = blobClient.GetBlobUri(blobName).ToString();

                var photo = OrderPhoto.Create(
                    orderId: command.OrderId,
                    photoType: photoToSave.PhotoType,
                    blobUrl: blobUrl,
                    fileName: uniqueFileName,
                    originalFileName: file.FileName,
                    fileSizeBytes: stream.Length,
                    contentType: contentType,
                    capturedByEmployeeId: employeeId,
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
            if (!string.IsNullOrEmpty(base64Content) && base64Content.StartsWith("data:"))
            {
                var contentType = base64Content.Split(';')[0].Replace("data:", "");
                if (!string.IsNullOrEmpty(contentType))
                    return contentType;
            }

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
