using System.Linq;
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
    public void Every_Feed_Key_Is_A_Known_Catalog_Const()
    {
        var catalogKeys = typeof(NotificationEventCatalog)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, FieldType: { } t } && t == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        foreach (var key in NotificationFeedEventKeys.Customer.Concat(NotificationFeedEventKeys.Partner))
        {
            Assert.True(catalogKeys.Contains(key),
                $"Feed key '{key}' is not a NotificationEventCatalog const — the keyset drifted from the catalog.");
        }
    }

    [Fact]
    public void The_Non_Mutable_Feed_Keys_Are_Exactly_Assignment_Cancelled_And_Invoice_Paid()
    {
        // A job cancellation and a payment confirmation must not be silenceable, so they map to no
        // category (the producer's mute gate is skipped). Every OTHER feed key stays mutable.
        string[] nonMutable =
        [
            NotificationEventCatalog.OrderAssignmentCancelled,
            NotificationEventCatalog.InvoicePaid,
        ];
        foreach (var key in nonMutable)
        {
            Assert.Null(NotificationEventCatalog.GetCategoryFor(key));
        }
        foreach (var key in NotificationFeedEventKeys.Customer
                     .Concat(NotificationFeedEventKeys.Partner)
                     .Where(k => !nonMutable.Contains(k)))
        {
            Assert.NotNull(NotificationEventCatalog.GetCategoryFor(key));
        }
    }

    [Fact]
    public void Customer_Keyset_Is_The_Eleven_Customer_Events()
    {
        Assert.Equal(11, NotificationFeedEventKeys.Customer.Count);
        Assert.Equal(NotificationFeedEventKeys.Customer.Count, NotificationFeedEventKeys.Customer.Distinct().Count());
    }

    [Fact]
    public void Partner_Keyset_Is_The_New_Jobs_Digest_Plus_Assignment_Cancelled_And_Invoice_Paid()
    {
        Assert.Equal(
            [
                NotificationEventCatalog.NewJobsAvailable,
                NotificationEventCatalog.OrderAssignmentCancelled,
                NotificationEventCatalog.InvoicePaid,
            ],
            NotificationFeedEventKeys.Partner);
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
