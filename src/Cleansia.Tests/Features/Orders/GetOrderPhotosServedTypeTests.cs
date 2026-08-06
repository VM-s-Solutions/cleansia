using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A write-path rule retypes nothing that is already stored, so this query still meets rows holding
/// whatever their uploader claimed — <c>image/tiff</c>, <c>image/bmp</c>, or anything else the old
/// signature table admitted. The signed URL has always clamped what such a row is SERVED as; the DTO
/// beside it emitted the raw recorded string, so a client was told one type and handed another.
///
/// <para>Both answers come from one <c>ServedContentType</c> resolution now. The clamp is what makes an
/// unrenderable legacy row report itself as an opaque download rather than as an image that never
/// appears.</para>
/// </summary>
public class GetOrderPhotosServedTypeTests
{
    private const string OrderId = "order-served-ct-1";
    private const string EmployeeId = "emp-served-ct-1";

    [Theory]
    [InlineData("image/tiff")]
    [InlineData("image/bmp")]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    public async Task A_Legacy_Unservable_Type_Is_Reported_As_Opaque(string recorded)
    {
        var photo = await FetchSinglePhotoAsync(recorded);

        Assert.Equal("application/octet-stream", photo.ContentType);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/jpeg")]
    public async Task A_Servable_Type_Is_Reported_Unchanged(string recorded)
    {
        var photo = await FetchSinglePhotoAsync(recorded);

        Assert.Equal(recorded, photo.ContentType);
    }

    /// <summary>
    /// The DTO and the signed URL must resolve the SAME value — one fact, one source. A test that only
    /// checked the DTO would go green again if the two were re-derived independently and drifted.
    /// </summary>
    [Fact]
    public async Task The_Reported_Type_Is_The_One_Pinned_On_The_Signed_Url()
    {
        ServedContentType? servedAs = null;

        var photo = await FetchSinglePhotoAsync("image/tiff", capture: s => servedAs = s);

        Assert.NotNull(servedAs);
        Assert.Equal(servedAs!.Value, photo.ContentType);
    }

    private static async Task<GetOrderPhotos.OrderPhotoDto> FetchSinglePhotoAsync(
        string recordedContentType,
        Action<ServedContentType>? capture = null)
    {
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.Completed, EmployeeId);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var photo = OrderPhoto.Create(
            orderId: OrderId,
            photoType: PhotoType.After,
            blobUrl: "https://account.blob.core.windows.net/order-photos/2026/order/photo.bin",
            fileName: "photo.bin",
            originalFileName: "photo.bin",
            fileSizeBytes: 1024,
            contentType: recordedContentType,
            capturedByEmployeeId: EmployeeId);

        var photoRepository = new Mock<IOrderPhotoRepository>();
        photoRepository
            .Setup(r => r.GetPhotosByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([photo]);

        var accessService = new Mock<IOrderAccessService>();
        accessService
            .Setup(s => s.CanBrowseOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<ServedContentType>()))
            .Returns<string, TimeSpan, ServedContentType>((name, _, served) =>
            {
                capture?.Invoke(served);
                return new Uri($"https://account.blob.core.windows.net/order-photos/{name}?sig=x");
            });

        var blobFactory = new Mock<IBlobContainerClientFactory>();
        blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        var handler = new GetOrderPhotos.Handler(
            orderRepository.Object, photoRepository.Object, accessService.Object, blobFactory.Object);

        var result = await handler.Handle(new GetOrderPhotos.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return Assert.Single(result.Value!.Photos);
    }
}
