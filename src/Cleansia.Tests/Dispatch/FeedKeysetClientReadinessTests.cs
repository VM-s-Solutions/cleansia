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
    public void The_Admin_Reassign_Events_Render_As_Push_But_Are_Not_Yet_Feed_Registered(string eventKey)
    {
        // Display registered 2026-08-09: both platforms carry the copy, so the push renders.
        // The FEED half stays held — iOS has no feed template, order-number arm or deep link for
        // these keys yet, and a keyset row the app drops is the unread count this class exists to
        // prevent. The wave that ships those three iOS sites deletes this test.
        Assert.Contains(eventKey, FcmMessageFactory.ApnsDisplayMap.Keys);
        Assert.False(NotificationFeedEventKeys.IsFeedEvent(eventKey));
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
    public void The_Pre_Cleaning_Reminder_Renders_As_Push_But_Is_Not_Yet_Feed_Registered()
    {
        // Display registered 2026-08-09; the feed half stays held for the same reason as above.
        Assert.Contains(NotificationEventCatalog.OrderStartingSoon, FcmMessageFactory.ApnsDisplayMap.Keys);
        Assert.False(NotificationFeedEventKeys.IsFeedEvent(NotificationEventCatalog.OrderStartingSoon));
    }

    /// <summary>
    /// The two per-job cleaner reminders render as push and are deliberately NOT feed events — unlike
    /// their sibling <see cref="NotificationEventCatalog.ReminderTomorrow"/>, which is.
    ///
    /// <para>This is the OPPOSITE of the holds above: the copy shipped on both platforms first, so the
    /// display registration is complete and intentional. What is withheld is the FEED row, because
    /// these are transient — a row per job would fill the badge with reminders about work that has
    /// already started. <c>order.starting_soon</c> is in neither keyset for exactly the same reason.
    /// Deleting this test to "finish" the pair would be the mistake it exists to prevent.</para>
    /// </summary>
    [Theory]
    [InlineData(NotificationEventCatalog.ReminderSoon)]
    [InlineData(NotificationEventCatalog.ReminderNotStarted)]
    public void The_Per_Job_Reminders_Render_As_Push_But_Are_Deliberately_Not_Feed_Events(string eventKey)
    {
        Assert.Contains(eventKey, FcmMessageFactory.ApnsDisplayMap.Keys);
        Assert.False(NotificationFeedEventKeys.IsFeedEvent(eventKey));
    }

    /// <summary>
    /// All three reminders are non-mutable, and nothing else pins the two that are not feed events —
    /// <see cref="NotificationFeedEventKeysTests"/> can only reach the one in the keyset.
    ///
    /// <para>Non-mutability here is an OMISSION: <c>GetCategoryFor</c> has no arm for these keys, so
    /// they fall through to null. That is the right default direction, but an omission is exactly what
    /// somebody "tidies up" later by adding the arm they assume was forgotten. A cleaner must not be
    /// able to silence a reminder about work they accepted and then not turn up.</para>
    /// </summary>
    [Theory]
    [InlineData(NotificationEventCatalog.ReminderTomorrow)]
    [InlineData(NotificationEventCatalog.ReminderSoon)]
    [InlineData(NotificationEventCatalog.ReminderNotStarted)]
    public void The_Job_Reminders_Are_Non_Mutable(string eventKey) =>
        Assert.Null(NotificationEventCatalog.GetCategoryFor(eventKey));

    /// <summary>
    /// ADR-0045 D10.2 — the one key that ADR mints is held out of both lists on the same terms: no
    /// customer client carries "your favourite didn't take it" copy yet, so the push is data-only on iOS
    /// and the inbox stays empty rather than counting a row no app can draw. The wave that ships
    /// <c>push.order.preferred_offer_closed.title|body</c> in both iOS catalogs and the Android template
    /// deletes this test, registers the display-map entry, and only then adds the key to the customer
    /// keyset.
    /// </summary>
    [Fact]
    public void The_Preferred_Offer_Closure_Renders_As_Push_But_Is_Not_Yet_Feed_Registered()
    {
        // Display registered 2026-08-09, once both platforms carried the copy (a21dfcc3 iOS,
        // b996c5d5 Android) — the precondition ApnsDisplayMapIosCatalogSyncTests enforces. The FEED
        // half stays held: no client has a feed template or a deep-link arm for this key, and a keyset
        // row the app drops is the inflated unread badge this class exists to prevent.
        Assert.Contains(NotificationEventCatalog.PreferredOfferClosed, FcmMessageFactory.ApnsDisplayMap.Keys);
        Assert.False(NotificationFeedEventKeys.IsFeedEvent(NotificationEventCatalog.PreferredOfferClosed));
    }

    /// <summary>
    /// Silenceable under the category the customer already has — an update about their own order must
    /// not need its own opt-out to be discoverable, and a new <see cref="NotificationCategory"/> is a
    /// bool COLUMN plus a toggle in every client.
    /// </summary>
    [Fact]
    public void The_Preferred_Offer_Closure_Is_Mutable_Under_Order_Updates() =>
        Assert.Equal(
            NotificationCategory.OrderUpdates,
            NotificationEventCatalog.GetCategoryFor(NotificationEventCatalog.PreferredOfferClosed));

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
