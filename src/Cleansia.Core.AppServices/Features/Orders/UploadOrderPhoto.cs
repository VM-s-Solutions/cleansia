using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

public class UploadOrderPhoto
{
    public record Command(
        string OrderId,
        string EmployeeId,
        PhotoType PhotoType,
        string FileName,
        string ContentType,
        byte[] FileData,
        string? Notes = null) : ICommand<Response>;

    public record Response(
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

            RuleFor(x => x.FileName)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(255)
                .WithMessage(BusinessErrorMessage.MaxLength);

            RuleFor(x => x.ContentType)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .Must(ct => AllowedContentTypes.Contains(ct.ToLower()))
                .WithMessage(BusinessErrorMessage.InvalidFileType);

            RuleFor(x => x.FileData)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.FileRequired)
                .Must(data => data.Length <= MaxFileSizeBytes)
                .WithMessage(BusinessErrorMessage.FileSizeExceeded);
        }

        private async Task<bool> EmployeeIsAssignedToOrderAsync(Command command, CancellationToken cancellationToken)
        {
            // Implementation will check if employee is assigned to the order
            return true; // Placeholder - will be implemented in handler
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

            // Generate unique filename with timestamp
            var fileExtension = Path.GetExtension(command.FileName);
            var uniqueFileName = $"{command.OrderId}_{command.PhotoType}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}{fileExtension}";
            var blobName = $"{DateTime.UtcNow.Year}/{command.OrderId}/{uniqueFileName}";

            // Upload to blob storage
            var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
            using var stream = new MemoryStream(command.FileData);
            var metadata = Metadata.CreateBuilder()
                .WithMetadata(MetadataName.ContentType, command.ContentType)
                .Build();
            await blobClient.UploadAsync(blobName, stream, metadata, cancellationToken);

            var blobUrl = blobClient.GetBlobUri(blobName).ToString();

            // Create photo entity
            var photo = OrderPhoto.Create(
                orderId: command.OrderId,
                photoType: command.PhotoType,
                blobUrl: blobUrl,
                fileName: uniqueFileName,
                originalFileName: command.FileName,
                fileSizeBytes: command.FileData.Length,
                contentType: command.ContentType,
                capturedByEmployeeId: command.EmployeeId,
                notes: command.Notes);

            photoRepository.Add(photo);

            return BusinessResult.Success(new Response(
                PhotoId: photo.Id,
                BlobUrl: blobUrl,
                PhotoType: command.PhotoType,
                CapturedAt: photo.CapturedAt));
        }
    }
}
