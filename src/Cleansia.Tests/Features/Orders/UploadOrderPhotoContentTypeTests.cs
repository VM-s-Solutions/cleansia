using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// <c>UploadOrderPhoto</c> recorded <c>command.ContentType</c> — a string the caller chose — onto the row
/// that <c>GetOrderPhotos</c> later resolves the served <c>Content-Type</c> from. Its declared-type
/// allowlist bounded what a caller may CLAIM and nothing about the bytes, so any payload under a
/// permitted claim was stored as that claim. The bytes decide now.
///
/// <para>This intake was invisible to the upload roster for two tickets because it takes a raw
/// <c>byte[]</c> rather than a <c>BlobFileDto</c> — see <c>UploadIntakeRosterTests</c>.</para>
/// </summary>
public class UploadOrderPhotoContentTypeTests
{
    private const string OrderId = "order-upload-ct-1";
    private const string EmployeeId = "emp-upload-ct-1";

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private static byte[] WebpBytes()
    {
        var bytes = new byte[64];
        "RIFF"u8.ToArray().CopyTo(bytes, 0);
        "WEBP"u8.ToArray().CopyTo(bytes, 8);
        return bytes;
    }

    [Fact]
    public async Task The_Recorded_Type_Comes_From_The_Bytes_Not_The_Declared_String()
    {
        var saved = await UploadAsync(PngBytes, declaredContentType: "image/webp", fileName: "shot.webp");

        Assert.Equal("image/png", saved.ContentType);
    }

    /// <summary>
    /// The blob name is minted from the same answer, so a caller cannot leave a <c>.webp</c> on a PNG and
    /// have a later reader resolve the type off the name.
    /// </summary>
    [Fact]
    public async Task The_Blob_Name_Extension_Comes_From_The_Bytes()
    {
        var saved = await UploadAsync(PngBytes, declaredContentType: "image/webp", fileName: "shot.webp");

        Assert.EndsWith(".png", saved.FileName);
    }

    /// <summary>
    /// What is recorded must be servable as itself, or the clamp on the read path would silently demote
    /// every genuine photo to octet-stream.
    /// </summary>
    [Fact]
    public async Task The_Recorded_Type_Survives_The_Read_Path_Clamp()
    {
        var saved = await UploadAsync(WebpBytes(), declaredContentType: "image/webp", fileName: "shot.webp");

        Assert.Equal(saved.ContentType, ServedContentType.ForRecordedType(saved.ContentType).Value);
    }

    [Theory]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 })] // RIFF….WAVE
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 })] // %PDF-1.7
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x20, 0x78, 0x6D, 0x6C })] // <svg xml
    public async Task A_Payload_That_Is_Not_An_Accepted_Image_Is_Refused(byte[] fileData)
    {
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var validator = new UploadOrderPhoto.Validator(orderRepository.Object);

        var result = await validator.ValidateAsync(
            new UploadOrderPhoto.Command(OrderId, PhotoType.Before, "shot.png", "image/png", fileData));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidFileType);
    }

    /// <summary>
    /// Every content type this intake's accepted set names, reached through a genuine payload of that
    /// format. A set member with no matching row in the signature table is unreachable — the intake would
    /// refuse a format the partner picker offers — and nothing but this pairing would say so.
    /// </summary>
    public static TheoryData<string, byte[]> AcceptedFormats() => new()
    {
        { "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01] },
        { "image/png", PngBytes },
        { "image/webp", WebpBytes() }
    };

    [Theory]
    [MemberData(nameof(AcceptedFormats))]
    public async Task Every_Format_The_Clients_Offer_Is_Recorded_As_Itself(string expected, byte[] fileData)
    {
        var saved = await UploadAsync(fileData, declaredContentType: "image/jpeg", fileName: "shot.jpg");

        Assert.Equal(expected, saved.ContentType);
    }

    [Fact]
    public async Task A_Genuine_Image_Passes_The_Validator()
    {
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var validator = new UploadOrderPhoto.Validator(orderRepository.Object);

        var result = await validator.ValidateAsync(
            new UploadOrderPhoto.Command(OrderId, PhotoType.Before, "shot.png", "image/png", PngBytes));

        Assert.True(result.IsValid);
    }

    private static async Task<OrderPhoto> UploadAsync(byte[] fileData, string declaredContentType, string fileName)
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

        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
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
        Assert.NotNull(captured);
        return captured!;
    }
}
