using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28: the server enforces one photo window on every client — a before photo from Confirmed
/// through InProgress, an after photo during InProgress — and a cleaner may delete a photo only until
/// the order is completed or cancelled, so the after photo a completion rested on cannot be removed.
/// Photo links live fifteen minutes.
/// </summary>
public sealed class OrderPhotoWindowTests
{
    private const string OrderId = "order-photo-window";
    private const string PhotoId = "photo-1";

    private readonly Mock<IOrderRepository> _orders = new();

    private void OrderAt(OrderStatus status)
    {
        _orders.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orders.Setup(r => r.GetCurrentStatusAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(status);
    }

    [Theory]
    [InlineData(PhotoType.Before, OrderStatus.New, false)]
    [InlineData(PhotoType.Before, OrderStatus.Confirmed, true)]
    [InlineData(PhotoType.Before, OrderStatus.OnTheWay, true)]
    [InlineData(PhotoType.Before, OrderStatus.InProgress, true)]
    [InlineData(PhotoType.Before, OrderStatus.Completed, false)]
    [InlineData(PhotoType.Before, OrderStatus.Cancelled, false)]
    [InlineData(PhotoType.After, OrderStatus.Confirmed, false)]
    [InlineData(PhotoType.After, OrderStatus.OnTheWay, false)]
    [InlineData(PhotoType.After, OrderStatus.InProgress, true)]
    [InlineData(PhotoType.After, OrderStatus.Completed, false)]
    [InlineData(PhotoType.After, OrderStatus.Cancelled, false)]
    [InlineData(PhotoType.Entrance, OrderStatus.New, false)]
    [InlineData(PhotoType.Entrance, OrderStatus.Confirmed, true)]
    [InlineData(PhotoType.Entrance, OrderStatus.OnTheWay, true)]
    [InlineData(PhotoType.Entrance, OrderStatus.InProgress, true)]
    [InlineData(PhotoType.Entrance, OrderStatus.Completed, false)]
    [InlineData(PhotoType.Entrance, OrderStatus.Cancelled, false)]
    public void Each_Photo_Type_Has_One_Window(PhotoType type, OrderStatus status, bool open)
    {
        Assert.Equal(open, OrderPhoto.MayBeAddedAt(type, status));
    }

    [Theory]
    [InlineData(PhotoType.Before, OrderStatus.Completed)]
    [InlineData(PhotoType.After, OrderStatus.OnTheWay)]
    [InlineData(PhotoType.After, OrderStatus.Cancelled)]
    public async Task An_Upload_Outside_Its_Window_Is_Refused(PhotoType type, OrderStatus status)
    {
        OrderAt(status);

        var result = await new UploadOrderPhoto.Validator(_orders.Object).ValidateAsync(
            new UploadOrderPhoto.Command(OrderId, type, "shot.png", "image/png", PngBytes));

        Assert.Equal(BusinessErrorMessage.OrderPhotoWindowClosed, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task An_Upload_Inside_Its_Window_Passes()
    {
        OrderAt(OrderStatus.InProgress);

        var result = await new UploadOrderPhoto.Validator(_orders.Object).ValidateAsync(
            new UploadOrderPhoto.Command(OrderId, PhotoType.After, "shot.png", "image/png", PngBytes));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Batch_Carrying_One_Photo_Outside_Its_Window_Is_Refused()
    {
        OrderAt(OrderStatus.OnTheWay);

        var result = await new SaveOrderPhotos.Validator(_orders.Object).ValidateAsync(new SaveOrderPhotos.Command(
            OrderId, [Photo(PhotoType.Before), Photo(PhotoType.After)]));

        Assert.Equal(BusinessErrorMessage.OrderPhotoWindowClosed, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Batch_Inside_Its_Windows_Passes()
    {
        OrderAt(OrderStatus.InProgress);

        var result = await new SaveOrderPhotos.Validator(_orders.Object).ValidateAsync(new SaveOrderPhotos.Command(
            OrderId, [Photo(PhotoType.Before), Photo(PhotoType.After)]));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.InProgress, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    public async Task A_Photo_Is_Deleted_Only_Before_The_Order_Is_Over(OrderStatus status, bool allowed)
    {
        OrderAt(status);
        var photos = new Mock<IOrderPhotoRepository>();
        photos.Setup(r => r.ExistsAsync(PhotoId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        photos.Setup(r => r.GetByIdAsync(PhotoId, It.IsAny<CancellationToken>())).ReturnsAsync(Stored(PhotoType.After));

        var result = await new DeleteOrderPhoto.Validator(photos.Object, _orders.Object)
            .ValidateAsync(new DeleteOrderPhoto.Command(PhotoId));

        if (allowed)
        {
            Assert.True(result.IsValid);
        }
        else
        {
            Assert.Equal(BusinessErrorMessage.OrderPhotoLocked, Assert.Single(result.Errors).ErrorMessage);
        }
    }

    [Fact]
    public async Task A_Photo_Link_Lives_Fifteen_Minutes()
    {
        var access = new Mock<IOrderAccessService>();
        access.Setup(s => s.IsCustomerCaller()).Returns(false);
        access
            .Setup(s => s.LoadOrderForCallerAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.InProgress, "emp-1", maxEmployees: 1));
        access.Setup(s => s.CanAccessOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var photos = new Mock<IOrderPhotoRepository>();
        photos.Setup(r => r.GetPhotosByOrderIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync([Stored(PhotoType.After)]);
        var lifetimes = new List<TimeSpan>();
        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<ServedContentType>()))
            .Returns<string, TimeSpan, ServedContentType>((name, lifetime, _) =>
            {
                lifetimes.Add(lifetime);
                return new Uri($"https://account.blob.core.windows.net/order-photos/{name}?sig=x");
            });
        var blobs = new Mock<IBlobContainerClientFactory>();
        blobs.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        var result = await new GetOrderPhotos.Handler(photos.Object, access.Object, blobs.Object)
            .Handle(new GetOrderPhotos.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMinutes(15), Assert.Single(lifetimes));
    }

    private static SaveOrderPhotos.PhotoToSave Photo(PhotoType type) =>
        new(type, new BlobFileDto("shot.jpg", Convert.ToBase64String(new byte[2048]), "image/jpeg"));

    private static OrderPhoto Stored(PhotoType type) => OrderPhoto.Create(
        orderId: OrderId,
        photoType: type,
        blobUrl: "https://account.blob.core.windows.net/order-photos/2026/order/photo.jpg",
        fileName: "photo.jpg",
        originalFileName: "photo.jpg",
        fileSizeBytes: 2048,
        contentType: "image/jpeg",
        capturedByEmployeeId: "emp-1");

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
}
