using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Orders;

public class GetOrderPhotos
{
    public record Query(string OrderId) : IQuery<Response>;

    public record Response(
        IEnumerable<OrderPhotoDto> Photos,
        int BeforePhotoCount,
        int AfterPhotoCount);

    public record OrderPhotoDto(
        string Id,
        PhotoType PhotoType,
        string BlobUrl,
        string FileName,
        string? OriginalFileName,
        long FileSizeBytes,
        string ContentType,
        DateTime CapturedAt,
        string? CapturedByEmployeeId,
        string? CapturedByEmployeeName,
        int? Width,
        int? Height,
        string? Notes);

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IOrderAccessService orderAccessService)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderAccessService.OrderExistsForCallerAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }
    }

    public class Handler(
        IOrderPhotoRepository photoRepository,
        IOrderAccessService orderAccessService,
        IBlobContainerClientFactory blobClientFactory) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            // Strict, not browse: every photo is an interior view of a private dwelling handed over as
            // a signed URL that works outside Cleansia auth and can be forwarded. Nothing about the
            // inside of the home is part of deciding whether to take the job, and the write paths were
            // already assignment-gated.
            var order = await orderAccessService.LoadOrderForCallerAsync(query.OrderId, cancellationToken);
            if (order == null || !await orderAccessService.CanAccessOrderAsync(order, cancellationToken))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(query.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var photos = orderAccessService.IsCustomerCaller()
                ? await photoRepository.GetPhotosByOrderIdForOwnerAsync(order.Id, order.UserId!, cancellationToken)
                : await photoRepository.GetPhotosByOrderIdAsync(order.Id, cancellationToken);
            var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
            var hideEmployeeIds = orderAccessService.IsCustomerCaller();

            var photoDtos = photos.Select(p => MapToDto(p, blobClient, hideEmployeeIds)).ToList();

            var beforeCount = photos.Count(p => p.PhotoType == PhotoType.Before);
            var afterCount = photos.Count(p => p.PhotoType == PhotoType.After);

            return BusinessResult.Success(new Response(
                Photos: photoDtos,
                BeforePhotoCount: beforeCount,
                AfterPhotoCount: afterCount));
        }

        /// <summary>
        /// The DTO's ContentType is the SAME answer as the header the SAS pins, never the raw recorded
        /// string. A row written before the intake sniffed still holds whatever its uploader claimed, so
        /// emitting that verbatim tells a client <c>image/tiff</c> about a blob that will arrive as
        /// <c>application/octet-stream</c> — one fact with two sources, and the client believes the one
        /// that is wrong.
        /// </summary>
        private static OrderPhotoDto MapToDto(OrderPhoto photo, IBlobContainerClient blobClient, bool hideEmployeeIds)
        {
            var servedAs = ServedContentType.ForRecordedType(photo.ContentType);

            return new OrderPhotoDto(
                Id: photo.Id,
                PhotoType: photo.PhotoType,
                BlobUrl: GenerateSasUrl(blobClient, photo.BlobUrl, servedAs),
                FileName: photo.FileName,
                OriginalFileName: photo.OriginalFileName,
                FileSizeBytes: photo.FileSizeBytes,
                ContentType: servedAs.Value,
                CapturedAt: photo.CapturedAt,
                CapturedByEmployeeId: hideEmployeeIds ? null : photo.CapturedByEmployeeId,
                CapturedByEmployeeName: hideEmployeeIds
                    ? photo.CapturedBy?.User?.FirstName
                    : (photo.CapturedBy != null
                        ? $"{photo.CapturedBy.User?.FirstName} {photo.CapturedBy.User?.LastName}".Trim()
                        : null),
                Width: photo.Width,
                Height: photo.Height,
                Notes: photo.Notes);
        }

        private static string GenerateSasUrl(IBlobContainerClient blobClient, string blobUrl, ServedContentType servedAs) =>
            blobClient.GenerateSasUri(OrderPhotoBlobName.FromUrl(blobUrl), TimeSpan.FromHours(1), servedAs).ToString();
    }
}
