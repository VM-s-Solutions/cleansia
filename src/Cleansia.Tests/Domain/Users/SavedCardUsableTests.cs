using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Users;

/// <summary>
/// A saved card guarantees a cash booking only while it is captured, not removed, and not past the end
/// of its expiry month.
/// </summary>
public class SavedCardUsableTests
{
    private static readonly DateTimeOffset LastMinuteOfMarch = new(2027, 3, 31, 23, 59, 0, TimeSpan.Zero);

    [Fact]
    public void A_Card_Is_Usable_To_The_End_Of_Its_Expiry_Month_And_No_Longer()
    {
        var card = Captured(expMonth: 3, expYear: 2027);

        Assert.True(card.IsUsableOn(LastMinuteOfMarch));
        Assert.False(card.IsUsableOn(LastMinuteOfMarch.AddMinutes(1)));
    }

    [Fact]
    public void A_Later_Expiry_Year_Is_Usable_Whatever_Its_Month_And_An_Earlier_One_Is_Not()
    {
        Assert.True(Captured(expMonth: 1, expYear: 2028).IsUsableOn(LastMinuteOfMarch));
        Assert.False(Captured(expMonth: 12, expYear: 2026).IsUsableOn(LastMinuteOfMarch));
    }

    [Fact]
    public void A_Card_Whose_Capture_Has_Not_Landed_Is_Not_Usable()
    {
        var pending = SavedCard.Start("user-usable", "currency-czk", "cus_usable", null, null);

        Assert.False(pending.IsUsableOn(LastMinuteOfMarch));
    }

    [Fact]
    public void A_Removed_Card_Is_Not_Usable()
    {
        var card = Captured(expMonth: 3, expYear: 2027);
        card.Deactivated("user-usable", LastMinuteOfMarch.AddDays(-1));

        Assert.False(card.IsUsableOn(LastMinuteOfMarch));
    }

    private static SavedCard Captured(int expMonth, int expYear)
    {
        var card = SavedCard.Start("user-usable", "currency-czk", "cus_usable", null, null);
        card.Capture("pm_usable", "visa", "4242", expMonth, expYear);
        return card;
    }
}
