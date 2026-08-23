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
    public void The_Non_Mutable_Feed_Keys_Are_Exactly_The_Three_A_Cleaner_Must_Not_Silence()
    {
        // A job cancellation, a payment confirmation, and the day-ahead schedule must not be
        // silenceable, so they map to no category (the producer's mute gate is skipped). Every OTHER
        // feed key stays mutable.
        //
        // The digest joined them because a cleaner who can turn off "tomorrow you have 2 jobs" can
        // turn off the thing that stops them forgetting — the same argument the catalog already makes
        // about a job appearing on their own schedule.
        string[] nonMutable =
        [
            NotificationEventCatalog.OrderAssignmentCancelled,
            NotificationEventCatalog.InvoicePaid,
            NotificationEventCatalog.ReminderTomorrow,
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
    public void Customer_Keyset_Is_Exactly_The_Customer_Targeted_Events()
    {
        // Asserts the LIST, not a count. A count in the assertion — and worse, in the test name — is
        // the thing a later author leaves stale, and what has to be re-checked on every addition is
        // which events a client can render, not how many there are. Same shape as the partner keyset
        // below, and the same reasoning FcmMessageFactoryTests states for the APNs display map.
        Assert.Equal(
            [
                NotificationEventCatalog.OrderConfirmed,
                NotificationEventCatalog.OrderCleanerAssigned,
                NotificationEventCatalog.OrderOnTheWay,
                NotificationEventCatalog.OrderInProgress,
                NotificationEventCatalog.OrderCompleted,
                NotificationEventCatalog.OrderCancelled,
                NotificationEventCatalog.OrderRefunded,
                NotificationEventCatalog.DisputeReply,
                NotificationEventCatalog.RecurringScheduled,
                NotificationEventCatalog.MembershipExpiringSoon,
                NotificationEventCatalog.MembershipCancellationEffective,
                NotificationEventCatalog.LoyaltyTierUpgrade,
            ],
            NotificationFeedEventKeys.Customer);
        Assert.Equal(NotificationFeedEventKeys.Customer.Count, NotificationFeedEventKeys.Customer.Distinct().Count());
    }

    [Fact]
    public void Partner_Keyset_Is_Exactly_The_Partner_Targeted_Events()
    {
        Assert.Equal(
            [
                NotificationEventCatalog.NewJobsAvailable,
                NotificationEventCatalog.ReminderTomorrow,
                NotificationEventCatalog.PreferredOffer,
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
