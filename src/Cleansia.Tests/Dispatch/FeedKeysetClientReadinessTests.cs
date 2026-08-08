using Cleansia.Core.Domain.Notifications;
using Cleansia.Infra.Clients.Fcm;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// The client-first rule, as an invariant between the two lists that encode it, rather than as a
/// sentence in a doc comment that each new event's author has to remember to read.
///
/// <para>The unread badge counts every row in a keyset and the apps drop what they cannot render, so a
/// key that enters a keyset ahead of its client templates inflates the badge with an invisible row —
/// the feed's most damaging failure, because it is indistinguishable from a delivery bug. Membership of
/// <see cref="FcmMessageFactory.ApnsDisplayMap"/> is the server's only machine-checkable proxy for
/// "the clients carry copy for this": <c>ApnsDisplayMapIosCatalogSyncTests</c> reads both iOS
/// catalogs off disk to prove it. Chaining the two means a premature keyset entry fails here, and the
/// only way to make it pass is to ship the copy.</para>
///
/// <para>The implication runs one way ONLY. A key may be push-only — dispatched and rendered with no
/// inbox row — which is what a new event looks like while its clients catch up. What it may not be is
/// inbox-visible and unrenderable.</para>
/// </summary>
public class FeedKeysetClientReadinessTests
{
    [Theory]
    [InlineData(NotificationFeedAudience.Customer)]
    [InlineData(NotificationFeedAudience.Partner)]
    public void No_Feed_Key_Outruns_Its_Client_Push_Copy(NotificationFeedAudience audience)
    {
        var unrenderable = NotificationFeedEventKeys.For(audience)
            .Where(key => !FcmMessageFactory.ApnsDisplayMap.ContainsKey(key))
            .ToList();

        Assert.True(
            unrenderable.Count == 0,
            $"{audience} feed keyset carries {string.Join(", ", unrenderable)} with no FcmMessageFactory.ApnsDisplayMap " +
            "entry. The badge counts these rows and the apps drop them unrendered. Ship the push copy in both iOS " +
            "catalogs, register the event in the display map, and only then add it to the keyset.");
    }

    /// <summary>
    /// The two admin-reassign events are held out of both lists on purpose: this is a backend-only wave
    /// and neither app carries copy for them yet. Named here so the hold reads as a decision rather than
    /// an omission — and so the wave that ships the copy has to delete this test deliberately, which is
    /// the moment to re-read the rule above.
    /// </summary>
    [Theory]
    [InlineData(NotificationEventCatalog.OrderAssigned)]
    [InlineData(NotificationEventCatalog.OrderAssignmentRevoked)]
    public void The_Admin_Reassign_Events_Are_Dispatched_But_Not_Yet_Feed_Or_Display_Registered(string eventKey)
    {
        Assert.False(NotificationFeedEventKeys.IsFeedEvent(eventKey));
        Assert.DoesNotContain(eventKey, FcmMessageFactory.ApnsDisplayMap.Keys);
    }

    /// <summary>
    /// The pre-cleaning reminder is held out of both lists for the same reason and on the same terms:
    /// the backend wave that makes the booking screen's "we'll remind you 1 hour before" true ships
    /// without client copy, so the push is data-only on iOS and the inbox stays empty rather than
    /// counting a row no app can draw. The wave that ships <c>push.order.starting_soon.title|body</c> in
    /// both iOS catalogs and the Android template deletes this test, registers the display map entry,
    /// and only then adds the key to the customer keyset.
    /// </summary>
    [Fact]
    public void The_Pre_Cleaning_Reminder_Is_Dispatched_But_Not_Yet_Feed_Or_Display_Registered()
    {
        Assert.False(NotificationFeedEventKeys.IsFeedEvent(NotificationEventCatalog.OrderStartingSoon));
        Assert.DoesNotContain(NotificationEventCatalog.OrderStartingSoon, FcmMessageFactory.ApnsDisplayMap.Keys);
    }

    /// <summary>
    /// It is silenceable, and under the category the customer already has: a new
    /// <see cref="NotificationCategory"/> is a bool COLUMN on <c>UserNotificationPreferences</c> plus a
    /// toggle in every client, and someone who muted order updates has already answered this question.
    /// </summary>
    [Fact]
    public void The_Pre_Cleaning_Reminder_Is_Mutable_Under_Order_Updates() =>
        Assert.Equal(
            NotificationCategory.OrderUpdates,
            NotificationEventCatalog.GetCategoryFor(NotificationEventCatalog.OrderStartingSoon));

    /// <summary>
    /// Both are operational notices about a cleaner's own working day, so neither is silenceable — the
    /// null category is what makes <c>SendPushNotificationHandler</c> skip the mute gate. Making either
    /// mutable is not a code change: a category is a bool COLUMN on <c>UserNotificationPreferences</c>,
    /// so it costs a migration and a toggle in both partner apps.
    /// </summary>
    [Theory]
    [InlineData(NotificationEventCatalog.OrderAssigned)]
    [InlineData(NotificationEventCatalog.OrderAssignmentRevoked)]
    public void The_Admin_Reassign_Events_Are_Non_Mutable(string eventKey) =>
        Assert.Null(NotificationEventCatalog.GetCategoryFor(eventKey));
}
