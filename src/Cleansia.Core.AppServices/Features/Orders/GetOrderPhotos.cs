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
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IOrderPhotoRepository photoRepository,
        IOrderAccessService orderAccessService,
        IBlobContainerClientFactory blobClientFactory) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetByIdAsync(query.OrderId, cancellationToken);
            if (order == null || !await orderAccessService.CanBrowseOrderAsync(order, cancellationToken))
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(query.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var photos = await photoRepository.GetPhotosByOrderIdAsync(query.OrderId, cancellationToken);
            var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);
            var hideEmployeeIds = orderAccessService.IsCustomerCaller();

            var photoDtos = photos.Select(p => MapToDto(p, blobClient, hideEmployeeIds)).ToList();

            var beforeCount = await photoRepository.GetPhotoCountByOrderIdAndTypeAsync(
                query.OrderId,
                PhotoType.Before,
                cancellationToken);

            var afterCount = await photoRepository.GetPhotoCountByOrderIdAndTypeAsync(
                query.OrderId,
                PhotoType.After,
                cancellationToken);

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

        private static string GenerateSasUrl(IBlobContainerClient blobClient, string blobUrl, ServedContentType servedAs)
        {
            // We need to recover the blob name (the path WITHIN the
            // container) from the stored absolute URL so we can hand
            // it to GenerateSasUri — which itself prepends the
            // container path. The previous Skip(1) only worked on the
            // production Azure URL shape `/<container>/<blob>`; on
            // Azurite the path is `/<accountName>/<container>/<blob>`
            // which left the container name in the blob name and
            // produced doubled paths like
            // `…/order-photos/order-photos/2026/…` in the SAS URL.
            //
            // Locate the container segment by name and take everything
            // after it. Works for both Azure (`/<container>/…`) and
            // Azurite (`/<account>/<container>/…`).
            var uri = new Uri(blobUrl);
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var containerName = Constants.BlobContainers.OrderPhotos;
            var containerIndex = Array.IndexOf(pathSegments, containerName);
            var blobName = containerIndex >= 0 && containerIndex + 1 < pathSegments.Length
                ? string.Join("/", pathSegments.Skip(containerIndex + 1))
                : string.Join("/", pathSegments.Skip(1)); // legacy fallback
            return blobClient.GenerateSasUri(blobName, TimeSpan.FromHours(1), servedAs).ToString();
        }
    }
}
