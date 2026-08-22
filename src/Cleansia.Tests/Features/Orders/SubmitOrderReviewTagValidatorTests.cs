using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The tag rules on <see cref="SubmitOrderReview"/>, which all REFUSE rather than silently dropping.
///
/// <para>Dropping would be the cheaper implementation and the wrong one: the stored review would then
/// say something the customer did not choose, and the client bug that sent the bad tag would never
/// surface. Every case below asserts the specific error key, because the three clients localize on the
/// key and a generic refusal renders as "An error occurred".</para>
/// </summary>
public class SubmitOrderReviewTagValidatorTests
{
    private const string OrderId = "order-1";

    private readonly SubmitOrderReview.Validator _validator;

    public SubmitOrderReviewTagValidatorTests()
    {
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _validator = new SubmitOrderReview.Validator(orderRepository.Object);
    }

    private Task<FluentValidation.Results.ValidationResult> Validate(
        int rating,
        IReadOnlyList<ReviewTag>? tags,
        string? comment = null) =>
        _validator.ValidateAsync(new SubmitOrderReview.Command(OrderId, rating, comment, tags));

    [Fact]
    public async Task A_review_with_no_tags_is_valid()
    {
        Assert.True((await Validate(5, null)).IsValid);
        Assert.True((await Validate(5, [])).IsValid);
    }

    [Fact]
    public async Task Positive_tags_are_accepted_at_four_and_five_stars()
    {
        Assert.True((await Validate(4, [ReviewTag.OnTime, ReviewTag.Thorough])).IsValid);
        Assert.True((await Validate(5, [ReviewTag.GreatPhotos])).IsValid);
    }

    [Fact]
    public async Task Negative_tags_are_accepted_at_one_to_three_stars()
    {
        Assert.True((await Validate(1, [ReviewTag.ArrivedLate])).IsValid);
        Assert.True((await Validate(3, [ReviewTag.MissedAreas, ReviewTag.FeltRushed])).IsValid);
    }

    [Theory]
    [InlineData(5, ReviewTag.ArrivedLate)]
    [InlineData(4, ReviewTag.MissedAreas)]
    [InlineData(1, ReviewTag.OnTime)]
    [InlineData(3, ReviewTag.Thorough)]
    public async Task A_tag_on_the_wrong_side_of_the_rating_is_refused(int rating, ReviewTag tag)
    {
        var result = await Validate(rating, [tag]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ReviewTagRatingMismatch);
    }

    [Fact]
    public async Task More_than_the_cap_is_refused()
    {
        var tooMany = ReviewTagPolarity.ForRating(5).Take(ReviewTagPolarity.MaxTagsPerReview + 1).ToList();

        Assert.True(
            tooMany.Count == ReviewTagPolarity.MaxTagsPerReview + 1,
            "The positive set is too small to exceed the cap — this case cannot fail vacuously.");

        var result = await Validate(5, tooMany);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ReviewTooManyTags);
    }

    [Fact]
    public async Task A_repeated_tag_is_refused()
    {
        var result = await Validate(5, [ReviewTag.OnTime, ReviewTag.OnTime]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ReviewDuplicateTag);
    }

    [Fact]
    public async Task A_value_outside_the_enum_is_refused()
    {
        var result = await Validate(5, [(ReviewTag)999]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ReviewUnknownTag);
    }

    /// <summary>
    /// The polarity arm reads Rating, so it must not fire on a rating that is itself invalid — that
    /// would report a confusing second failure alongside the real one.
    /// </summary>
    [Fact]
    public async Task An_invalid_rating_reports_only_the_rating()
    {
        var result = await Validate(9, [ReviewTag.OnTime]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ReviewRatingInvalid);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.ReviewTagRatingMismatch);
    }
}
