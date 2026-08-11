using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The last private copy of the 10 MiB upload limit, folded onto <c>Common/Validators/BlobFileSize</c>.
///
/// <para>The two derivations agree exactly — <c>(long)(n * 0.75)</c> and <c>(n * 3L) / 4L</c> are both
/// <c>floor(3n/4)</c> for every <c>int</c> length, and <c>0.75</c> is exactly representable — so there is
/// no payload size that the copy and the shared predicate disagree about. What the copy cost was not a
/// wrong answer but a second place for the number to be changed, which is the whole of the rule.</para>
///
/// <para>The one behavioural difference is at zero: the shared predicate treats blank content as
/// out-of-bounds rather than as size 0, so blank now fails BOTH rules and only the order decides which
/// message the caller sees. That is why the blank case asserts the message and not merely the rejection.</para>
/// </summary>
public class SaveOrderPhotosSizeBoundTests
{
    private const long TenMebibytes = 10L * 1024 * 1024;
    private const string OrderId = "order-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();

    public SaveOrderPhotosSizeBoundTests() =>
        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

    private SaveOrderPhotos.Validator CreateValidator() => new(_orderRepository.Object);

    private static SaveOrderPhotos.Command CommandWith(string? base64Content) =>
        new(OrderId, [new SaveOrderPhotos.PhotoToSave(
            PhotoType.Before,
            new BlobFileDto("shot.jpg", base64Content, "image/jpeg"))]);

    private static string Payload(long size) => Convert.ToBase64String(new byte[size]);

    [Fact]
    public async Task Photo_Within_The_Limit_Passes()
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(Payload(TenMebibytes - 1024)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Photo_Over_TenMebibytes_Fails_With_FileSizeExceeded()
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(Payload(TenMebibytes + 1024)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileSizeExceeded);
    }

    /// <summary>
    /// The shared predicate rejects blank content, so a chain that ran it first would report a missing
    /// photo as an oversized one. Asserting the single message is what makes the ORDER of the two rules
    /// the thing under test; asserting only that blank is refused would survive deleting either.
    /// </summary>
    [Fact]
    public async Task Blank_Content_Reports_FileRequired_Rather_Than_FileSizeExceeded()
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(string.Empty));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.FileRequired, failure.ErrorMessage);
    }
}
