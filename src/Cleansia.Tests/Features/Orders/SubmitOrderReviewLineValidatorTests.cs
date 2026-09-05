using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The SHAPE rules on per-item review scores.
///
/// <para>Deliberately only shape. Whether an item is actually on the order is the handler's job,
/// after the ownership gate and against the graph it already loads — a membership rule here would run
/// BEFORE that gate and answer "is service X on order Y" for an order the caller does not own, which
/// is precisely the enumeration difference the handler's not-found-not-forbidden answer exists to
/// deny. <c>CreateDispute</c> splits the same work the same way, for the same reason.</para>
/// </summary>
public class SubmitOrderReviewLineValidatorTests
{
    private const string OrderId = "order-1";

    private readonly SubmitOrderReview.Validator _validator;

    public SubmitOrderReviewLineValidatorTests()
    {
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _validator = new SubmitOrderReview.Validator(orderRepository.Object);
    }

    private Task<FluentValidation.Results.ValidationResult> ValidateAsync(
        params SubmitOrderReview.ReviewLineScore[] lines) =>
        _validator.ValidateAsync(
            new SubmitOrderReview.Command(OrderId, Rating: 4, Comment: null, Tags: null, Lines: lines));

    [Fact]
    public async Task NoLinesIsOrdinary()
    {
        var result = await _validator.ValidateAsync(
            new SubmitOrderReview.Command(OrderId, Rating: 4, Comment: null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AScorePerItemIsAccepted()
    {
        var result = await ValidateAsync(
            new SubmitOrderReview.ReviewLineScore("svc-oven", null, 5),
            new SubmitOrderReview.ReviewLineScore("svc-bath", null, 2));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task AScoreOutsideOneToFiveIsRefused(int rating)
    {
        var result = await ValidateAsync(new SubmitOrderReview.ReviewLineScore("svc-oven", null, rating));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ReviewRatingInvalid);
    }

    /// <summary>
    /// Two scores for one item is a client bug, and refusing says so. SetLines replaces wholesale, so
    /// accepting both would silently keep whichever landed last.
    /// </summary>
    [Fact]
    public async Task TwoScoresForTheSameItemAreRefused()
    {
        var result = await ValidateAsync(
            new SubmitOrderReview.ReviewLineScore("svc-oven", null, 5),
            new SubmitOrderReview.ReviewLineScore("svc-oven", null, 1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ReviewDuplicateTag);
    }

    /// <summary>
    /// The SAME service bought standalone and inside a package is two different items, and both may
    /// be scored. The identity is the pair, not the service id.
    /// </summary>
    [Fact]
    public async Task TheSameServiceStandaloneAndInAPackageAreDistinctItems()
    {
        var result = await ValidateAsync(
            new SubmitOrderReview.ReviewLineScore("svc-oven", null, 5),
            new SubmitOrderReview.ReviewLineScore("svc-oven", "pkg-deep", 1));

        Assert.True(result.IsValid);
    }
}
