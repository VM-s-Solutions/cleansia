using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Order photos are interior views of a customer's home, and each one is handed over as a one-hour
/// signed URL — a bearer capability that works outside Cleansia auth and is forwardable. The query used
/// to gate on the loose browse rule, so any cleaner with a resolvable employee id could fetch every
/// photo of any order that still had a free seat, together with the shooting cleaner's id and full
/// name. The write paths (<c>UploadOrderPhoto</c>, <c>SaveOrderPhotos</c>) were already
/// assignment-gated; the read is now too.
/// </summary>
public class GetOrderPhotosAssignmentGateTests
{
    private const string OrderId = "order-photo-gate-1";
    private const string AssignedEmployeeId = "employee-photo-gate";

    [Fact]
    public async Task A_Caller_The_Strict_Gate_Refuses_Gets_No_Photos_And_No_Signed_Urls()
    {
        var minted = new List<string>();

        var result = await FetchAsync(canAccess: false, minted);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.Empty(minted);
    }

    [Fact]
    public async Task A_Caller_The_Strict_Gate_Admits_Still_Gets_The_Photos()
    {
        var minted = new List<string>();

        var result = await FetchAsync(canAccess: true, minted);

        Assert.True(result.IsSuccess);
        var photo = Assert.Single(result.Value!.Photos);
        Assert.Equal(AssignedEmployeeId, photo.CapturedByEmployeeId);
        Assert.Single(minted);
    }

    /// <summary>
    /// The loose gate must not be consulted at all — a handler that fell back to it on refusal would
    /// pass the test above and still serve every browsing cleaner.
    /// </summary>
    [Fact]
    public async Task The_Browse_Gate_Is_Not_Consulted()
    {
        var accessService = new Mock<IOrderAccessService>(MockBehavior.Strict);
        accessService
            .Setup(s => s.CanAccessOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await FetchAsync(accessService, new List<string>());

        Assert.False(result.IsSuccess);
        accessService.Verify(
            s => s.CanBrowseOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Task<BusinessResult<GetOrderPhotos.Response>> FetchAsync(bool canAccess, List<string> minted)
    {
        var accessService = new Mock<IOrderAccessService>();
        accessService
            .Setup(s => s.CanAccessOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canAccess);
        accessService
            .Setup(s => s.CanBrowseOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return FetchAsync(accessService, minted);
    }

    private static async Task<BusinessResult<GetOrderPhotos.Response>> FetchAsync(
        Mock<IOrderAccessService> accessService, List<string> minted)
    {
        accessService.Setup(s => s.IsCustomerCaller()).Returns(false);

        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.Completed, AssignedEmployeeId, maxEmployees: 2);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var photo = OrderPhoto.Create(
            orderId: OrderId,
            photoType: PhotoType.After,
            blobUrl: "https://account.blob.core.windows.net/order-photos/2026/order/photo.jpg",
            fileName: "photo.jpg",
            originalFileName: "photo.jpg",
            fileSizeBytes: 2048,
            contentType: "image/jpeg",
            capturedByEmployeeId: AssignedEmployeeId);

        var photoRepository = new Mock<IOrderPhotoRepository>();
        photoRepository
            .Setup(r => r.GetPhotosByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([photo]);
        photoRepository
            .Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(
                OrderId, It.IsAny<PhotoType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<ServedContentType>()))
            .Returns<string, TimeSpan, ServedContentType>((name, _, _) =>
            {
                minted.Add(name);
                return new Uri($"https://account.blob.core.windows.net/order-photos/{name}?sig=x");
            });

        var blobFactory = new Mock<IBlobContainerClientFactory>();
        blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        var handler = new GetOrderPhotos.Handler(
            orderRepository.Object, photoRepository.Object, accessService.Object, blobFactory.Object);

        return await handler.Handle(new GetOrderPhotos.Query(OrderId), CancellationToken.None);
    }
}
