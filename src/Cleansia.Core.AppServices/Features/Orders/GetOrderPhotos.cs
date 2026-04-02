using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
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
        string CapturedByEmployeeId,
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
        IOrderPhotoRepository photoRepository,
        IBlobContainerClientFactory blobClientFactory) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var photos = await photoRepository.GetPhotosByOrderIdAsync(query.OrderId, cancellationToken);
            var blobClient = blobClientFactory.GetBlobContainerClient(Constants.BlobContainers.OrderPhotos);

            var photoDtos = photos.Select(p => new OrderPhotoDto(
                Id: p.Id,
                PhotoType: p.PhotoType,
                BlobUrl: GenerateSasUrl(blobClient, p.BlobUrl),
                FileName: p.FileName,
                OriginalFileName: p.OriginalFileName,
                FileSizeBytes: p.FileSizeBytes,
                ContentType: p.ContentType,
                CapturedAt: p.CapturedAt,
                CapturedByEmployeeId: p.CapturedByEmployeeId,
                CapturedByEmployeeName: p.CapturedBy != null
                    ? $"{p.CapturedBy.User?.FirstName} {p.CapturedBy.User?.LastName}".Trim()
                    : null,
                Width: p.Width,
                Height: p.Height,
                Notes: p.Notes
            )).ToList();

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

        private static string GenerateSasUrl(IBlobContainerClient blobClient, string blobUrl)
        {
            // Extract blob name from the stored URL
            // URL format: https://<account>.blob.core.windows.net/<container>/<blobName>
            var uri = new Uri(blobUrl);
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Skip the first segment (container name), rejoin the rest as the blob name
            var blobName = string.Join("/", pathSegments.Skip(1));

            return blobClient.GenerateSasUri(blobName, TimeSpan.FromHours(1)).ToString();
        }
    }
}
