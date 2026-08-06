using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Media;
using Cleansia.Core.AppServices.Common.Validators;
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
        private const long MaxFileSizeBytes = 10 * 1024 * 1024;

        // A client-affordance filter, not a control: it bounds what a caller may CLAIM, and arbitrary
        // bytes under a permitted claim pass it unchanged. The FileData rule below is the control.
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/jpg", "image/png", "image/webp" };

        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);

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
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.FileRequired)
                .Must(data => data.Length <= MaxFileSizeBytes)
                .WithMessage(BusinessErrorMessage.FileSizeExceeded)
                .Must(data => SniffedContentType.FromContent(data, UploadIntake.OrderPhoto) is not null)
                .WithMessage(BusinessErrorMessage.InvalidFileType);
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

            var contentType = SniffedContentType.FromContent(command.FileData, UploadIntake.OrderPhoto)!;
            var uniqueFileName = $"{command.OrderId}_{command.PhotoType}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}{SniffedContentType.ExtensionFor(contentType)}";
            var blobName = $"{DateTime.UtcNow.Year}/{command.OrderId}/{uniqueFileName}";

            var storedBytes = ImageMetadata.Scrub(command.FileData).Bytes;

            var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
            using var stream = new MemoryStream(storedBytes);
            await blobClient.UploadAsync(blobName, stream, cancellationToken: cancellationToken);

            var blobUrl = blobClient.GetBlobUri(blobName).ToString();

            var photo = OrderPhoto.Create(
                orderId: command.OrderId,
                photoType: command.PhotoType,
                blobUrl: blobUrl,
                fileName: uniqueFileName,
                originalFileName: command.FileName,
                fileSizeBytes: storedBytes.Length,
                contentType: contentType,
                capturedByEmployeeId: employeeId,
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
