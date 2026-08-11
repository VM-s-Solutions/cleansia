using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The collection half of the photo bound, and the last of the three base64 intakes to get one. The
/// per-photo size rule bounds ONE item; the list it sits on was unbounded, and the request body divided
/// by a SMALL item is thousands of blob uploads and rows in a single request.
///
/// <para>The cap is thirty where both document intakes use ten, and the two tests bracket it from both
/// sides on purpose — a batch AT the cap must pass, one over it must not — so the number itself is
/// pinned rather than merely "some cap exists".</para>
/// </summary>
public class SaveOrderPhotosCountBoundTests
{
    private const int Cap = 30;
    private const string OrderId = "order-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();

    public SaveOrderPhotosCountBoundTests() =>
        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

    private SaveOrderPhotos.Validator CreateValidator() => new(_orderRepository.Object);

    private static SaveOrderPhotos.PhotoToSave Photo(string? base64Content) =>
        new(PhotoType.Before, new BlobFileDto("shot.jpg", base64Content, "image/jpeg"));

    private static SaveOrderPhotos.Command CommandOf(int count, string? base64Content) =>
        new(OrderId, [.. Enumerable.Range(0, count).Select(_ => Photo(base64Content))]);

    private static string Payload() => Convert.ToBase64String(new byte[2048]);

    [Fact]
    public async Task A_Batch_At_The_Cap_Passes()
    {
        var result = await CreateValidator().ValidateAsync(CommandOf(Cap, Payload()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task More_Photos_Than_The_Cap_Fails_With_FileCountExceeded()
    {
        var result = await CreateValidator().ValidateAsync(CommandOf(Cap + 1, Payload()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileCountExceeded);
    }

    /// <summary>
    /// The collection-level mirror of the per-file ordering rule: an over-long list must be refused
    /// WITHOUT every one of its items being validated first, which is the cost the cap exists to refuse.
    /// Every item here also fails a per-item rule, so a second error in the result means the item rules
    /// ran on a list that was already refused.
    /// </summary>
    [Fact]
    public async Task Over_Long_List_Is_Refused_Without_Validating_Its_Items()
    {
        var result = await CreateValidator().ValidateAsync(CommandOf(Cap + 1, string.Empty));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.FileCountExceeded, failure.ErrorMessage);
    }
}
