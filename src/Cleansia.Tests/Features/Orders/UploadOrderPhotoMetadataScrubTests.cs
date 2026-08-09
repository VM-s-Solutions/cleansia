using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Tests.Common.Media;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0043 D4 on the single-photo order intake. What an order photo discloses is the uploading
/// CLEANER's device identity and, for anything shot away from the job, that cleaner's own location.
/// <c>GetOrderPhotos</c> now gates on <c>CanAccessOrderAsync</c>, so the fetch set is enumerable —
/// the customer, the rest of the crew, and administrators — but it is still not the uploader alone,
/// which is the answer that would retire the scrub. A device serial is a stable cross-order
/// correlation key, so it walks straight through the two controls that deliberately withhold cleaner
/// identity from a fetcher.
/// </summary>
public class UploadOrderPhotoMetadataScrubTests
{
    private const string OrderId = "order-scrub-1";
    private const string EmployeeId = "emp-scrub-1";

    [Fact]
    public async Task A_Job_Site_Photograph_Reaches_Storage_Without_Its_Coordinates_Or_Its_Camera()
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6), SyntheticJpeg.IptcSegment());

        var upload = await UploadAsync(photo, "before.jpg", "image/jpeg");

        Assert.False(ByteSequence.Contains(upload.Stored, SyntheticJpeg.GpsCoordinateBytes()));
        Assert.False(ByteSequence.Contains(upload.Stored, SyntheticJpeg.DeviceSentinel));
        Assert.True(ByteSequence.Contains(upload.Stored, SyntheticJpeg.ImageBody));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public async Task The_Callers_Declared_Type_Never_Chooses_The_Walker(string declaredType)
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6));

        var upload = await UploadAsync(photo, "before.png", declaredType);

        Assert.False(ByteSequence.Contains(upload.Stored, SyntheticJpeg.GpsCoordinateBytes()));
    }

    /// <summary>
    /// The row records the size of what was stored. Recording the pre-scrub length would put a number on
    /// the row that no blob in the container has.
    /// </summary>
    [Fact]
    public async Task The_Recorded_Size_Is_The_Size_Of_What_Was_Stored()
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6));

        var upload = await UploadAsync(photo, "before.jpg", "image/jpeg");

        Assert.Equal(upload.Stored.Length, (int)upload.Photo.FileSizeBytes);
        Assert.True(upload.Stored.Length < photo.Length);
    }

    [Fact]
    public async Task A_Png_Loses_Its_Text_And_Exif_Chunks()
    {
        var image = SyntheticPng.Image(SyntheticPng.ExifChunk(), SyntheticPng.CompressedTextChunk());

        var upload = await UploadAsync(image, "before.png", "image/png");

        Assert.False(ByteSequence.Contains(upload.Stored, SyntheticPng.LocationSentinel));
        Assert.False(ByteSequence.Contains(upload.Stored, SyntheticPng.DeviceSentinel));
    }

    [Fact]
    public async Task A_WebP_Loses_Its_Exif_Chunk()
    {
        var image = SyntheticWebP.File(
            SyntheticWebP.Vp8x(SyntheticWebP.ExifFlag),
            SyntheticWebP.ImageData(),
            SyntheticWebP.ExifChunk());

        var upload = await UploadAsync(image, "before.webp", "image/webp");

        Assert.False(ByteSequence.Contains(upload.Stored, SyntheticWebP.LocationSentinel));
    }

    private static async Task<(byte[] Stored, OrderPhoto Photo)> UploadAsync(
        byte[] fileData, string fileName, string declaredContentType)
    {
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.InProgress, EmployeeId);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        OrderPhoto? captured = null;
        var photoRepository = new Mock<IOrderPhotoRepository>();
        photoRepository.Setup(r => r.Add(It.IsAny<OrderPhoto>())).Callback<OrderPhoto>(p => captured = p);

        var accessService = new Mock<IOrderAccessService>();
        accessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);

        byte[]? uploaded = null;
        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, Metadata?, CancellationToken>((_, stream, _, _) =>
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                uploaded = copy.ToArray();
            })
            .Returns(Task.CompletedTask);
        blobClient
            .Setup(c => c.GetBlobUri(It.IsAny<string>()))
            .Returns<string>(name => new Uri($"https://account.blob.core.windows.net/order-photos/{name}"));

        var blobFactory = new Mock<IBlobContainerClientFactory>();
        blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        var handler = new UploadOrderPhoto.Handler(
            orderRepository.Object, photoRepository.Object, accessService.Object, blobFactory.Object);

        var result = await handler.Handle(
            new UploadOrderPhoto.Command(OrderId, PhotoType.Before, fileName, declaredContentType, fileData),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(uploaded);
        Assert.NotNull(captured);
        return (uploaded!, captured!);
    }
}
