using Cleansia.Core.Domain.Notifications;

namespace Cleansia.Tests.Features.Notifications;

/// <summary>
/// Catalog closure for the feed audience keysets: every feed-scoped key resolves to a real
/// <see cref="NotificationCategory"/> (so the dispatch consumer's mute check and the clients'
/// template lookup can never meet an unmapped key), the two keysets are disjoint (a dual-role
/// user's rows partition cleanly per host), and <c>promo.new_sitewide</c> stays excluded from
/// feed v1.
/// </summary>
public class NotificationFeedEventKeysTests
{
    [Fact]
    public void Every_Feed_Key_Resolves_To_A_Notification_Category()
    {
        foreach (var key in NotificationFeedEventKeys.Customer.Concat(NotificationFeedEventKeys.Partner))
        {
            Assert.True(
                NotificationEventCatalog.GetCategoryFor(key) is not null,
                $"Feed key '{key}' does not resolve to a NotificationCategory — the keyset drifted from the catalog.");
        }
    }

    [Fact]
    public void Customer_Keyset_Is_The_Eleven_Customer_Events()
    {
        Assert.Equal(11, NotificationFeedEventKeys.Customer.Count);
        Assert.Equal(NotificationFeedEventKeys.Customer.Count, NotificationFeedEventKeys.Customer.Distinct().Count());
    }

    [Fact]
    public void Partner_Keyset_Is_Exactly_The_New_Jobs_Digest()
    {
        Assert.Equal([NotificationEventCatalog.NewJobsAvailable], NotificationFeedEventKeys.Partner);
    }

    [Fact]
    public void Keysets_Are_Disjoint()
    {
        Assert.Empty(NotificationFeedEventKeys.Customer.Intersect(NotificationFeedEventKeys.Partner));
    }

    [Fact]
    public void Promo_Is_Not_A_Feed_Event()
    {
        Assert.False(NotificationFeedEventKeys.IsFeedEvent(NotificationEventCatalog.PromoNewSitewide));
    }

    [Fact]
    public void For_Maps_Each_Audience_To_Its_Keyset()
    {
        Assert.Same(NotificationFeedEventKeys.Customer, NotificationFeedEventKeys.For(NotificationFeedAudience.Customer));
        Assert.Same(NotificationFeedEventKeys.Partner, NotificationFeedEventKeys.For(NotificationFeedAudience.Partner));
    }
}
