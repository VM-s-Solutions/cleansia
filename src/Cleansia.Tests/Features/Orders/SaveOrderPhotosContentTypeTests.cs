using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// <c>SaveOrderPhotos</c> is the one upload pipeline with no content-type allowlist anywhere: its
/// validator checks the file NAME and the payload SIZE, and the type is read straight off the client's
/// own <c>data:</c> URI prefix and stored on the row. Its siblings <c>UploadOrderPhoto</c> and
/// <c>UploadDisputeEvidence</c> both check theirs.
///
/// <para>That was harmless only while the stored type went nowhere — the blob was always served
/// <c>application/octet-stream</c> because the "ContentType" metadata constant never touched a real HTTP
/// header. The moment the served type is pinned from the record, the recorded value becomes a header a
/// caller chose, and <c>image/svg+xml</c> or <c>text/html</c> served from the storage account is stored
/// XSS on a host shared by every tenant. So the recorded value is canonicalized on the way in as well as
/// clamped on the way out: two independent reasons a caller cannot name its own header.</para>
/// </summary>
public class SaveOrderPhotosContentTypeTests
{
    private const string OrderId = "order-photo-ct-1";
    private const string EmployeeId = "emp-photo-ct-1";

    [Theory]
    [InlineData("data:text/html;base64,", "shot.jpg")]
    [InlineData("data:image/svg+xml;base64,", "shot.jpg")]
    [InlineData("data:application/javascript;base64,", "shot.png")]
    public async Task A_Script_Bearing_DataUri_Type_Is_Never_What_Gets_Recorded(string dataUriPrefix, string fileName)
    {
        var saved = await SavePhotoAsync(dataUriPrefix, fileName);

        Assert.DoesNotContain("html", saved.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("svg", saved.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript", saved.ContentType, StringComparison.OrdinalIgnoreCase);

        // And what IS recorded must be servable as itself, or the clamp on the read path would silently
        // demote every genuine photo to octet-stream.
        Assert.Equal(saved.ContentType, ServedContentType.ForRecordedType(saved.ContentType).Value);
    }

    [Theory]
    [InlineData("data:image/png;base64,", "shot.jpg", "image/png")]
    [InlineData("data:image/webp;base64,", "shot.jpg", "image/webp")]
    // No data-URI prefix: the extension decides, exactly as before.
    [InlineData("", "shot.png", "image/png")]
    public async Task A_Genuine_Image_Type_Survives_Unchanged(string dataUriPrefix, string fileName, string expected)
    {
        var saved = await SavePhotoAsync(dataUriPrefix, fileName);

        Assert.Equal(expected, saved.ContentType);
    }

    private static async Task<OrderPhoto> SavePhotoAsync(string dataUriPrefix, string fileName)
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

        var handler = new SaveOrderPhotos.Handler(
            orderRepository.Object, photoRepository.Object, accessService.Object, blobFactory.Object);

        var command = new SaveOrderPhotos.Command(
            OrderId,
            [new SaveOrderPhotos.PhotoToSave(
                PhotoType.Before,
                new BlobFileDto(fileName, dataUriPrefix + "iVBORw0KGgo=", null))]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        return captured!;
    }
}
