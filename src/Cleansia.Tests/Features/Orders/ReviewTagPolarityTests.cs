using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The chip polarity rule, as a property of the enum's numbering rather than a list anyone maintains.
///
/// <para>Positive tags occupy 1–10 and negative 11–20 (<see cref="ReviewTag"/>). That convention is the
/// whole mechanism: a tag added in the right band lands on the correct side with no edit here, and one
/// added in the wrong band fails <see cref="Every_tag_sits_in_exactly_one_polarity_band"/> naming
/// itself. Three clients render these sets, so a tag that drifts between bands would silently offer
/// "arrived late" beside five stars.</para>
/// </summary>
public class ReviewTagPolarityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Low_ratings_offer_only_negative_tags(int rating)
    {
        var offered = ReviewTagPolarity.ForRating(rating);

        Assert.NotEmpty(offered);
        Assert.All(offered, tag => Assert.True(
            ReviewTagPolarity.IsNegative(tag),
            $"{tag} was offered at {rating} stars but is a positive tag."));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void High_ratings_offer_only_positive_tags(int rating)
    {
        var offered = ReviewTagPolarity.ForRating(rating);

        Assert.NotEmpty(offered);
        Assert.All(offered, tag => Assert.True(
            ReviewTagPolarity.IsPositive(tag),
            $"{tag} was offered at {rating} stars but is a negative tag."));
    }

    /// <summary>
    /// The floor: the two sets together must account for every declared value, so a tag can never be
    /// invisible to both ratings — which is what a numbering mistake would produce.
    /// </summary>
    [Fact]
    public void Every_tag_sits_in_exactly_one_polarity_band()
    {
        var all = Enum.GetValues<ReviewTag>();
        var positive = ReviewTagPolarity.ForRating(5);
        var negative = ReviewTagPolarity.ForRating(1);

        Assert.Equal(all.Length, positive.Count + negative.Count);
        Assert.Empty(positive.Intersect(negative));
        Assert.NotEmpty(positive);
        Assert.NotEmpty(negative);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void A_rating_outside_one_to_five_offers_nothing(int rating) =>
        Assert.Empty(ReviewTagPolarity.ForRating(rating));

    /// <summary>
    /// An enum member's integer IS the wire contract — three generated clients decode it — so a
    /// renumbering silently reinterprets every stored review. Pinned by value, not by name.
    /// </summary>
    [Theory]
    [InlineData(ReviewTag.OnTime, 1)]
    [InlineData(ReviewTag.Thorough, 2)]
    [InlineData(ReviewTag.Friendly, 3)]
    [InlineData(ReviewTag.CarefulWithBelongings, 4)]
    [InlineData(ReviewTag.ExtrasDoneWell, 5)]
    [InlineData(ReviewTag.FollowedInstructions, 6)]
    [InlineData(ReviewTag.GreatPhotos, 7)]
    [InlineData(ReviewTag.ArrivedLate, 11)]
    [InlineData(ReviewTag.MissedAreas, 12)]
    [InlineData(ReviewTag.FeltRushed, 13)]
    [InlineData(ReviewTag.ExtraNotDone, 14)]
    [InlineData(ReviewTag.DidNotFollowInstructions, 15)]
    [InlineData(ReviewTag.Unprofessional, 16)]
    [InlineData(ReviewTag.SmellOrProducts, 17)]
    [InlineData(ReviewTag.CrewSmallerThanBooked, 18)]
    public void Wire_values_are_frozen(ReviewTag tag, int expected) => Assert.Equal(expected, (int)tag);

    /// <summary>
    /// Damage must not be reachable as a tag: it belongs to the dispute path (ADR-0006, ADR-0009),
    /// which produces a refund. A chip absorbing it into a review would give the customer the feeling
    /// of having reported it and none of the mechanism.
    /// </summary>
    [Fact]
    public void No_tag_describes_damage()
    {
        var damageish = Enum.GetNames<ReviewTag>()
            .Where(name => name.Contains("Damage", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Broke", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Stole", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Theft", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            damageish.Count == 0,
            "Damage and theft are dispute reasons, not review tags — they sit on the money path and a "
                + "tag would silently swallow a refundable claim. Found: " + string.Join(", ", damageish));
    }
}
