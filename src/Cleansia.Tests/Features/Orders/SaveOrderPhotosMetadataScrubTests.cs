using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
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
/// ADR-0043 D4 on the batch order-photo intake — the route both mobile clients call and the one that
/// reads no byte of its payload to decide what it stores. That last part is why D2.2 exists: this
/// pipeline's recorded content type comes from the caller's own <c>data:</c> URI prefix, so a scrub
/// dispatching on it would be a no-op the uploader selects by editing one word.
///
/// <para>The scrub takes the decoded bytes instead, and the tests below send each format under a
/// deliberately wrong <c>data:</c> prefix and a deliberately wrong file extension.</para>
/// </summary>
public class SaveOrderPhotosMetadataScrubTests
{
    private const string OrderId = "order-batch-scrub-1";
    private const string EmployeeId = "emp-batch-scrub-1";

    [Fact]
    public async Task A_Job_Site_Photograph_Reaches_Storage_Without_Its_Coordinates_Or_Its_Camera()
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6), SyntheticJpeg.XmpSegment());

        var stored = await SaveAsync(("before.jpg", "data:image/jpeg;base64,", photo));

        Assert.False(ByteSequence.Contains(stored[0], SyntheticJpeg.GpsCoordinateBytes()));
        Assert.False(ByteSequence.Contains(stored[0], SyntheticJpeg.DeviceSentinel));
        Assert.True(ByteSequence.Contains(stored[0], SyntheticJpeg.ImageBody));
    }

    /// <summary>
    /// The uploader-selected no-op, made concrete: JPEG bytes under a PNG data URI and a PNG file name.
    /// Everything this pipeline records about the type says PNG; the walker still has to be the JPEG one.
    /// </summary>
    [Theory]
    [InlineData("data:image/png;base64,", "before.png")]
    [InlineData("data:image/webp;base64,", "before.webp")]
    [InlineData("", "before.png")]
    public async Task Neither_The_Data_Uri_Prefix_Nor_The_File_Name_Chooses_The_Walker(string prefix, string fileName)
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6));

        var stored = await SaveAsync((fileName, prefix, photo));

        Assert.False(ByteSequence.Contains(stored[0], SyntheticJpeg.GpsCoordinateBytes()));
    }

    [Fact]
    public async Task Every_Photo_In_A_Batch_Is_Scrubbed_Not_Just_The_First()
    {
        var jpeg = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6));
        var png = SyntheticPng.Image(SyntheticPng.ExifChunk());
        var webp = SyntheticWebP.File(
            SyntheticWebP.Vp8x(SyntheticWebP.ExifFlag), SyntheticWebP.ImageData(), SyntheticWebP.ExifChunk());

        var stored = await SaveAsync(
            ("before.jpg", "data:image/jpeg;base64,", jpeg),
            ("during.png", "data:image/png;base64,", png),
            ("after.webp", "data:image/webp;base64,", webp));

        Assert.Equal(3, stored.Count);
        Assert.False(ByteSequence.Contains(stored[0], SyntheticJpeg.GpsCoordinateBytes()));
        Assert.False(ByteSequence.Contains(stored[1], SyntheticPng.LocationSentinel));
        Assert.False(ByteSequence.Contains(stored[2], SyntheticWebP.LocationSentinel));
    }

    private static async Task<List<byte[]>> SaveAsync(params (string FileName, string Prefix, byte[] Bytes)[] photos)
    {
        var order = ValidatorTestHelpers.BuildOrder(OrderId, OrderStatus.InProgress, EmployeeId);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        var photoRepository = new Mock<IOrderPhotoRepository>();

        var accessService = new Mock<IOrderAccessService>();
        accessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);

        var uploaded = new List<byte[]>();
        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, Metadata?, CancellationToken>((_, stream, _, _) =>
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                uploaded.Add(copy.ToArray());
            })
            .Returns(Task.CompletedTask);
        blobClient
            .Setup(c => c.GetBlobUri(It.IsAny<string>()))
            .Returns<string>(name => new Uri($"https://account.blob.core.windows.net/order-photos/{name}"));

        var blobFactory = new Mock<IBlobContainerClientFactory>();
        blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        var handler = new SaveOrderPhotos.Handler(
            orderRepository.Object, photoRepository.Object, accessService.Object, blobFactory.Object);

        var command = new SaveOrderPhotos.Command(
            OrderId,
            photos.Select(photo => new SaveOrderPhotos.PhotoToSave(
                PhotoType.Before,
                new BlobFileDto(photo.FileName, photo.Prefix + Convert.ToBase64String(photo.Bytes), null))).ToList());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        return uploaded;
    }
}
