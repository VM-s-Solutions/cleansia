using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Moq;
using Cleansia.Tests.Infrastructure;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28 (D67): an assigned cleaner's access to the customer ends 24 hours after
/// completion and at once on cancellation. The detail is pinned in
/// <see cref="OrderDetailBrowsingCleanerRedactionTests"/>; these pin the two other reads that name the
/// customer or show the inside of their home — the receipt, which carries their name and address, and
/// the photos, of which a past crew keeps only the ones they took.
/// </summary>
public sealed class CrewAccessWindowSurfaceTests
{
    private const string OrderId = "order-crew-window";
    private const string CrewEmployeeId = "emp-crew";
    private const string OtherCrewEmployeeId = "emp-other-crew";
    private const string CustomerUserId = "user-customer-window";

    private readonly Mock<IOrderAccessService> _orderAccess = new();

    // ── The receipt ──

    [Theory]
    [InlineData(-25)]
    [InlineData(null)]
    public async Task Past_The_Window_The_Crew_Is_Answered_Order_Not_Found_For_The_Receipt(int? completedHoursAgo)
    {
        var order = completedHoursAgo is { } hours ? Completed(hours) : Cancelled();
        AsCrew(order);

        var result = await ReceiptValidator().ValidateAsync(new DownloadOrderReceipt.Query(OrderId));

        Assert.Equal(BusinessErrorMessage.OrderNotFound, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Inside_The_Window_The_Crew_Still_Downloads_The_Receipt()
    {
        AsCrew(Completed(-23));

        var result = await ReceiptValidator().ValidateAsync(new DownloadOrderReceipt.Query(OrderId));

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task The_Customer_Downloads_Their_Receipt_Long_After_The_Crews_Window()
    {
        var order = Completed(-24 * 30);
        _orderAccess.Setup(a => a.IsCustomerCaller()).Returns(true);
        Orders(order);

        var result = await ReceiptValidator().ValidateAsync(new DownloadOrderReceipt.Query(OrderId));

        Assert.True(result.IsValid, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>D71: the PDF of a receipt past its retention period is deleted; the row stays as the record.</summary>
    [Fact]
    public async Task A_Receipt_Whose_Pdf_Was_Deleted_Is_Not_Found()
    {
        var order = Completed(-24 * 30);
        order.Receipt!.MarkBlobDeleted(DateTime.UtcNow.AddDays(-1));
        _orderAccess.Setup(a => a.IsCustomerCaller()).Returns(true);
        Orders(order);

        var result = await ReceiptValidator().ValidateAsync(new DownloadOrderReceipt.Query(OrderId));

        Assert.Equal(BusinessErrorMessage.ReceiptNotFound, Assert.Single(result.Errors).ErrorMessage);
    }

    // ── The photos ──

    [Fact]
    public async Task Past_The_Window_A_Crew_Member_Keeps_Only_The_Photos_They_Took()
    {
        var photos = await PhotosAsCrewAsync(Completed(-25));

        var photo = Assert.Single(photos.Photos);
        Assert.Equal(CrewEmployeeId, photo.CapturedByEmployeeId);
        Assert.Equal(1, photos.AfterPhotoCount);
    }

    [Fact]
    public async Task Inside_The_Window_A_Crew_Member_Sees_Every_Photo_Of_The_Job()
    {
        var photos = await PhotosAsCrewAsync(Completed(-23));

        Assert.Equal(2, photos.Photos.Count());
    }

    [Fact]
    public async Task A_Cancelled_Job_Leaves_A_Crew_Member_Only_Their_Own_Photos()
    {
        var photos = await PhotosAsCrewAsync(Cancelled());

        Assert.Equal(CrewEmployeeId, Assert.Single(photos.Photos).CapturedByEmployeeId);
    }

    private async Task<GetOrderPhotos.Response> PhotosAsCrewAsync(Order order)
    {
        AsCrew(order);
        _orderAccess.Setup(a => a.LoadOrderForCallerAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orderAccess.Setup(a => a.CanAccessOrderAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var photoRepository = new Mock<IOrderPhotoRepository>();
        photoRepository
            .Setup(r => r.GetPhotosByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Photo(CrewEmployeeId), Photo(OtherCrewEmployeeId)]);

        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<ServedContentType>()))
            .Returns<string, TimeSpan, ServedContentType>((name, _, _) => new Uri($"https://account.blob.core.windows.net/order-photos/{name}?sig=x"));
        var blobFactory = new Mock<IBlobContainerClientFactory>();
        blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        var result = await new GetOrderPhotos.Handler(photoRepository.Object, _orderAccess.Object, blobFactory.Object)
            .Handle(new GetOrderPhotos.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static OrderPhoto Photo(string capturedByEmployeeId) =>
        OrderPhoto.Create(
            orderId: OrderId,
            photoType: PhotoType.After,
            blobUrl: $"https://account.blob.core.windows.net/order-photos/2026/{capturedByEmployeeId}.jpg",
            fileName: $"{capturedByEmployeeId}.jpg",
            originalFileName: $"{capturedByEmployeeId}.jpg",
            fileSizeBytes: 2048,
            contentType: "image/jpeg",
            capturedByEmployeeId: capturedByEmployeeId);

    private DownloadOrderReceipt.Validator ReceiptValidator() => new(_orderAccess.Object);

    private void AsCrew(Order order)
    {
        _orderAccess.Setup(a => a.IsCustomerCaller()).Returns(false);
        _orderAccess.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CrewEmployeeId);
        Orders(order);
    }

    private void Orders(Order order)
    {
        _orderAccess.Setup(a => a.OrdersForCaller()).Returns(() => new[] { order }.AsQueryable().BuildMock());
        _orderAccess
            .Setup(a => a.OrderExistsForCallerAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private static Order Completed(int completedHoursAgo)
    {
        var order = WithReceipt(ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.Completed, CrewEmployeeId, maxEmployees: 2, userId: CustomerUserId));
        order.MarkCompletedAt(DateTime.UtcNow.AddHours(completedHoursAgo));
        return order;
    }

    private static Order Cancelled() =>
        WithReceipt(ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.Cancelled, CrewEmployeeId, maxEmployees: 2, userId: CustomerUserId));

    // Receipt has no domain writer on the order; EF materialises it. Reflection is the only way to
    // arrange one, and without it the receipt rule would refuse every case for the wrong reason.
    private static Order WithReceipt(Order order)
    {
        OrderReceiptAttachment.Attach(
            order, OrderReceipt.Create(OrderId, "CZ-2026-000321", "receipt.pdf", "blob", "en"));
        return order;
    }
}
