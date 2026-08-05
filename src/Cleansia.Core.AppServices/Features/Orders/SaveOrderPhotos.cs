using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
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
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .WithMessage(BusinessErrorMessage.Required)
                    .MaximumLength(255)
                    .WithMessage(BusinessErrorMessage.MaxLength);

                // The presence rule has to stay ahead of the size rule and cannot be folded into it:
                // the shared predicate treats blank content as out of bounds, so the order is the only
                // thing deciding whether a missing photo reports itself as an oversized one.
                photo.RuleFor(p => p.File)
                    .Cascade(CascadeMode.Stop)
                    .Must(file => !string.IsNullOrWhiteSpace(file.Base64Content))
                    .WithMessage(BusinessErrorMessage.FileRequired)
                    .Must(BlobFileSize.HasContentWithinLimit)
                    .WithMessage(BusinessErrorMessage.FileSizeExceeded);
            });
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
                await blobClient.UploadAsync(blobName, stream, cancellationToken: cancellationToken);

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

        /// <summary>
        /// The client's own <c>data:</c> URI prefix is a hint, never the answer: it is an arbitrary
        /// caller string, and this value is stored on the row and later pinned onto the served
        /// <c>Content-Type</c>. Resolving it through <see cref="ServedContentType"/> means the worst a
        /// caller can achieve is the opaque default, so a <c>data:image/svg+xml</c> or
        /// <c>data:text/html</c> upload cannot put its own type on a header.
        /// </summary>
        private static string DetermineContentType(string fileName, string? base64Content)
        {
            if (!string.IsNullOrEmpty(base64Content) && base64Content.StartsWith("data:"))
            {
                var declared = ServedContentType.ForRecordedType(base64Content.Split(';')[0].Replace("data:", ""));
                if (declared != ServedContentType.Opaque)
                {
                    return declared.Value;
                }
            }

            var byExtension = ServedContentType.ForFileName(fileName);
            return byExtension == ServedContentType.Opaque ? "image/jpeg" : byExtension.Value;
        }
    }
}
