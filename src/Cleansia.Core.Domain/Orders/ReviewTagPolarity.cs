using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// Which <see cref="ReviewTag"/> values belong to which rating, evaluated in one place so the server
/// gate and the three clients' chip lists cannot disagree.
///
/// <para>The boundary is <b>4 stars</b>: 1–3 offers the negative set, 4–5 the positive one. It is a
/// property of the value, not a lookup table — positive tags are numbered 1–10 and negative ones 11–20
/// (see <see cref="ReviewTag"/>), so a new tag lands on the correct side by being numbered in the right
/// band and needs no edit here.</para>
///
/// <para>A mismatch is refused rather than silently dropped: a client that offers "arrived late" beside
/// five stars has a bug, and swallowing it would leave the review saying something the customer did not
/// mean.</para>
/// </summary>
public static class ReviewTagPolarity
{
    /// <summary>The lowest rating that offers the positive set.</summary>
    public const int PositiveRatingFloor = 4;

    /// <summary>The most tags one review may carry — a chip list, not a questionnaire.</summary>
    public const int MaxTagsPerReview = 4;

    private const int NegativeBandStart = 11;

    public static bool IsPositive(ReviewTag tag) => (int)tag < NegativeBandStart;

    public static bool IsNegative(ReviewTag tag) => !IsPositive(tag);

    /// <summary>True when <paramref name="tag"/> may be attached to a review carrying <paramref name="rating"/>.</summary>
    public static bool MatchesRating(ReviewTag tag, int rating) =>
        rating >= PositiveRatingFloor ? IsPositive(tag) : IsNegative(tag);

    /// <summary>The set a client should offer for a rating. Empty for a rating outside 1–5.</summary>
    public static IReadOnlyList<ReviewTag> ForRating(int rating) =>
        rating is < 1 or > 5
            ? []
            : [.. Enum.GetValues<ReviewTag>().Where(tag => MatchesRating(tag, rating))];
}
