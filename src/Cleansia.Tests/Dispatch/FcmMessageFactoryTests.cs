using Cleansia.Infra.Clients.Fcm;
using FirebaseAdmin.Messaging;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// ADR-0025 wire-shape contract for <see cref="FcmMessageFactory"/> (TC-PUSH-APNS-0…5).
/// The Android half — the data-only payload + <see cref="AndroidConfig"/> — is pinned
/// byte-identical to the pre-ADR dispatcher shape; the APNs half — derived
/// <c>push.&lt;event_key&gt;.title|body</c> loc-keys with ordered, allowlisted loc-args,
/// default sound, immediate priority, thread-id grouping — is attached iff the event key
/// is in the display map. Unmapped keys (promo, unknown) ship data-only: drop-parity with
/// Android's unknown-key behavior.
/// </summary>
public class FcmMessageFactoryTests
{
    private static readonly string[] Tokens = ["TOKEN-1", "TOKEN-2"];

    private static Dictionary<string, string> OrderArgs() => new()
    {
        ["orderId"] = "ord-1",
        ["orderNumber"] = "A-1042",
    };

    // ── TC-PUSH-APNS-0 — per-event alert: derived loc-keys + exact ordered args ──────────────

    public static TheoryData<string, Dictionary<string, string>, string[]> DisplayableEvents => new()
    {
        { "order.confirmed", OrderArgs(), ["A-1042"] },
        { "order.on_the_way", OrderArgs(), ["A-1042"] },
        { "order.in_progress", OrderArgs(), ["A-1042"] },
        { "order.completed", OrderArgs(), ["A-1042"] },
        { "order.cancelled", OrderArgs(), ["A-1042"] },
        { "order.refunded", OrderArgs(), ["A-1042"] },
        { "recurring.scheduled", OrderArgs(), ["A-1042"] },
        { "order.new_available", new Dictionary<string, string> { ["count"] = "3" }, ["3"] },
        { "dispute.reply", new Dictionary<string, string> { ["orderId"] = "ord-1", ["disputeId"] = "dsp-1" }, [] },
        { "loyalty.tier_upgrade", new Dictionary<string, string> { ["tier"] = "SilverMopper" }, [] },
        { "membership.expiring_soon", new Dictionary<string, string>(), [] },
        { "membership.cancellation_effective", new Dictionary<string, string>(), [] },
    };

    [Theory]
    [MemberData(nameof(DisplayableEvents))]
    public void Mapped_Event_Alert_Carries_Derived_Loc_Keys_And_Exact_Ordered_Args(
        string eventKey, Dictionary<string, string> data, string[] expectedLocArgs)
    {
        var message = FcmMessageFactory.Build(Tokens, eventKey, data);

        Assert.NotNull(message.Apns);
        var alert = message.Apns.Aps?.Alert;
        Assert.NotNull(alert);
        Assert.Equal($"push.{eventKey}.title", alert.TitleLocKey);
        Assert.Equal($"push.{eventKey}.body", alert.LocKey);
        Assert.Equal(expectedLocArgs, alert.LocArgs);
    }

    // ── TC-PUSH-APNS-1 — Android regression pin: Data + AndroidConfig byte-identical to today ──

    public static TheoryData<string, Dictionary<string, string>> AllProducedEvents => new()
    {
        { "order.confirmed", OrderArgs() },
        { "order.on_the_way", OrderArgs() },
        { "order.in_progress", OrderArgs() },
        { "order.completed", OrderArgs() },
        { "order.cancelled", OrderArgs() },
        { "order.refunded", OrderArgs() },
        { "recurring.scheduled", OrderArgs() },
        { "order.new_available", new Dictionary<string, string> { ["count"] = "3" } },
        { "dispute.reply", new Dictionary<string, string> { ["orderId"] = "ord-1", ["disputeId"] = "dsp-1" } },
        { "loyalty.tier_upgrade", new Dictionary<string, string> { ["tier"] = "SilverMopper" } },
        { "membership.expiring_soon", new Dictionary<string, string>() },
        { "membership.cancellation_effective", new Dictionary<string, string>() },
        { "promo.new_sitewide", new Dictionary<string, string> { ["title"] = "Spring sale", ["body"] = "20% off this week" } },
    };

    [Theory]
    [MemberData(nameof(AllProducedEvents))]
    public void Every_Event_Keeps_Todays_Data_Payload_And_Android_Config(
        string eventKey, Dictionary<string, string> args)
    {
        var message = FcmMessageFactory.Build(Tokens, eventKey, args);

        AssertDataEquals(new Dictionary<string, string>(args) { ["event_key"] = eventKey }, message.Data);
        Assert.Equal(Tokens, message.Tokens);
        Assert.Null(message.Notification);

        Assert.NotNull(message.Android);
        Assert.Equal(Priority.High, message.Android.Priority);
        Assert.Null(message.Android.TimeToLive);
        Assert.Null(message.Android.CollapseKey);
        Assert.Null(message.Android.RestrictedPackageName);
        Assert.Null(message.Android.Data);
        Assert.Null(message.Android.Notification);

        if (message.Apns is not null)
        {
            Assert.False(message.Apns.Aps.ContentAvailable);
            Assert.False(message.Apns.Aps.MutableContent);
        }
    }

    [Fact]
    public void Data_Payload_Matches_The_Frozen_PreAdr_Wire_Shape_Exactly()
    {
        var message = FcmMessageFactory.Build(
            Tokens,
            "order.confirmed",
            new Dictionary<string, string> { ["orderId"] = "ord-1", ["orderNumber"] = "A-1042" });

        AssertDataEquals(
            new Dictionary<string, string>
            {
                ["orderId"] = "ord-1",
                ["orderNumber"] = "A-1042",
                ["event_key"] = "order.confirmed",
            },
            message.Data);
    }

    // ── TC-PUSH-APNS-2 — drop parity: unmapped keys ship data-only, Data intact ──────────────

    public static TheoryData<string, Dictionary<string, string>> UnmappedEvents => new()
    {
        // promo.new_sitewide is NO LONGER here — it now gets its own literal-alert branch (T-0412);
        // a genuinely unknown future event still ships data-only.
        { "totally.unknown_future_event", new Dictionary<string, string> { ["orderId"] = "ord-1" } },
    };

    [Theory]
    [MemberData(nameof(UnmappedEvents))]
    public void Unmapped_Event_Gets_No_Apns_Block_And_Keeps_Its_Data(
        string eventKey, Dictionary<string, string> args)
    {
        var message = FcmMessageFactory.Build(Tokens, eventKey, args);

        Assert.Null(message.Apns);
        AssertDataEquals(new Dictionary<string, string>(args) { ["event_key"] = eventKey }, message.Data);
    }

    // ── TC-PROMO-1 — the sitewide-promo literal pass-through (marketing posture) ──────────────

    [Fact]
    public void Promo_Carries_Literal_Title_Body_At_Passive_Priority_Five_With_No_Sound()
    {
        var message = FcmMessageFactory.Build(
            Tokens,
            "promo.new_sitewide",
            new Dictionary<string, string> { ["title"] = "Spring sale", ["body"] = "20% off this week" });

        Assert.NotNull(message.Apns);
        // Literal title/body — the admin authored them; NOT loc-keys.
        Assert.Equal("Spring sale", message.Apns!.Aps.Alert.Title);
        Assert.Equal("20% off this week", message.Apns.Aps.Alert.Body);
        Assert.Null(message.Apns.Aps.Alert.TitleLocKey);
        Assert.Null(message.Apns.Aps.Alert.LocKey);
        // Marketing posture: opportune delivery, passive, silent.
        Assert.Equal("5", message.Apns.Headers["apns-priority"]);
        Assert.Equal("passive", message.Apns.Aps.CustomData["interruption-level"]);
        Assert.Null(message.Apns.Aps.Sound);
        Assert.Equal("promo.new_sitewide", message.Apns.Aps.ThreadId);
        // Data payload still intact for the in-app feed.
        AssertDataEquals(
            new Dictionary<string, string> { ["title"] = "Spring sale", ["body"] = "20% off this week", ["event_key"] = "promo.new_sitewide" },
            message.Data);
    }

    [Theory]
    [InlineData("", "has body")]
    [InlineData("has title", "")]
    [InlineData("   ", "has body")]
    public void Promo_With_Blank_Title_Or_Body_Ships_Data_Only(string title, string body)
    {
        var message = FcmMessageFactory.Build(
            Tokens,
            "promo.new_sitewide",
            new Dictionary<string, string> { ["title"] = title, ["body"] = body });

        // Drop-parity with Android's isNotBlank guard — no half-rendered alert.
        Assert.Null(message.Apns);
    }

    // ── TC-PROMO-2 (tripwire) — ONLY promo.new_sitewide gets a LITERAL alert ──────────────────

    [Fact]
    public void No_Event_Other_Than_Promo_Produces_A_Literal_Title_Alert()
    {
        // Guards the event-scoped decision: a future producer that happens to send title/body must
        // NOT silently start displaying on iOS. Every catalog event carries title+body here; only
        // promo.new_sitewide may surface them as a literal Alert.Title — everyone else is either
        // data-only or a loc-key alert.
        foreach (var eventKey in AllCatalogEventKeys())
        {
            var message = FcmMessageFactory.Build(
                Tokens,
                eventKey,
                new Dictionary<string, string> { ["title"] = "x", ["body"] = "y", ["orderNumber"] = "A-1", ["count"] = "1" });

            if (eventKey == "promo.new_sitewide")
            {
                Assert.NotNull(message.Apns);
                Assert.Equal("x", message.Apns!.Aps.Alert.Title);
            }
            else if (message.Apns is not null)
            {
                // Mapped loc-key events use LocKeys, never a literal Title.
                Assert.Null(message.Apns.Aps.Alert.Title);
                Assert.NotNull(message.Apns.Aps.Alert.TitleLocKey);
            }
        }
    }

    // ── TC-PROMO-3 — APNs 4096-byte budget with worst-case (Cyrillic ×2 on the wire) ──────────

    [Fact]
    public void Promo_Alert_Stays_Within_The_Apns_Payload_Budget_At_Max_Length_Cyrillic()
    {
        // Admin title/body are length-capped; Cyrillic is 2 bytes/char in UTF-8 AND the text now
        // rides the wire twice (aps.alert + the data payload the feed reads), so pin the worst case.
        var title = new string('Я', 120);
        var body = new string('Ж', 800);
        var message = FcmMessageFactory.Build(
            Tokens,
            "promo.new_sitewide",
            new Dictionary<string, string> { ["title"] = title, ["body"] = body });

        var apsBytes = System.Text.Encoding.UTF8.GetByteCount(title) + System.Text.Encoding.UTF8.GetByteCount(body);
        var dataBytes = System.Text.Encoding.UTF8.GetByteCount(title) + System.Text.Encoding.UTF8.GetByteCount(body)
                        + System.Text.Encoding.UTF8.GetByteCount("promo.new_sitewide");
        // Even doubled, well under Apple's 4096-byte alert-push ceiling.
        Assert.True(apsBytes + dataBytes < 4096, $"payload {apsBytes + dataBytes}B exceeds the APNs budget");
        Assert.NotNull(message.Apns);
    }

    private static IEnumerable<string> AllCatalogEventKeys() =>
        typeof(Cleansia.Core.Domain.Notifications.NotificationEventCatalog)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, FieldType: { } t } && t == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    // ── TC-PUSH-APNS-3 — missing-arg tolerance: empty-string substitution, never dropped ─────

    [Fact]
    public void Refunded_Without_OrderNumber_Substitutes_Empty_String_And_Never_Drops_The_Alert()
    {
        var message = FcmMessageFactory.Build(
            Tokens,
            "order.refunded",
            new Dictionary<string, string> { ["orderId"] = "ord-1", ["disputeId"] = "dsp-1" });

        var alert = message.Apns?.Aps?.Alert;
        Assert.NotNull(alert);
        Assert.Equal(new[] { string.Empty }, alert.LocArgs);
    }

    // ── TC-PUSH-APNS-4 — aps furniture: sound, immediate priority header, thread-id chain ────

    [Fact]
    public void Alert_Ships_Default_Sound_And_Immediate_Apns_Priority_Header()
    {
        var message = FcmMessageFactory.Build(Tokens, "order.confirmed", OrderArgs());

        Assert.NotNull(message.Apns);
        Assert.Equal("default", message.Apns.Aps.Sound);
        Assert.Equal("10", message.Apns.Headers["apns-priority"]);
    }

    public static TheoryData<string, Dictionary<string, string>, string> ThreadIdCases => new()
    {
        { "order.confirmed", OrderArgs(), "ord-1" },
        { "dispute.reply", new Dictionary<string, string> { ["orderId"] = "ord-1", ["disputeId"] = "dsp-1" }, "ord-1" },
        { "dispute.reply", new Dictionary<string, string> { ["disputeId"] = "dsp-1" }, "dsp-1" },
        { "membership.expiring_soon", new Dictionary<string, string>(), "membership.expiring_soon" },
    };

    [Theory]
    [MemberData(nameof(ThreadIdCases))]
    public void ThreadId_Groups_By_OrderId_Then_DisputeId_Then_EventKey(
        string eventKey, Dictionary<string, string> data, string expectedThreadId)
    {
        var message = FcmMessageFactory.Build(Tokens, eventKey, data);

        Assert.NotNull(message.Apns);
        Assert.Equal(expectedThreadId, message.Apns.Aps.ThreadId);
    }

    // ── TC-PUSH-APNS-5 — S6 tripwire: lock-screen args stay inside the closed allowlist ──────

    [Fact]
    public void Display_Map_Arg_Names_Stay_Within_The_OrderNumber_Count_Allowlist()
    {
        var allowlist = new[] { "orderNumber", "count" };

        var mappedArgNames = FcmMessageFactory.ApnsDisplayMap.Values
            .SelectMany(argNames => argNames)
            .Distinct()
            .ToList();

        Assert.All(mappedArgNames, argName => Assert.Contains(argName, allowlist));
    }

    [Fact]
    public void Display_Map_Contains_Exactly_The_Twelve_Ratified_Events()
    {
        string[] expected =
        [
            "dispute.reply",
            "loyalty.tier_upgrade",
            "membership.cancellation_effective",
            "membership.expiring_soon",
            "order.cancelled",
            "order.completed",
            "order.confirmed",
            "order.in_progress",
            "order.new_available",
            "order.on_the_way",
            "order.refunded",
            "recurring.scheduled",
        ];

        Assert.Equal(expected, FcmMessageFactory.ApnsDisplayMap.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    private static void AssertDataEquals(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (key, value) in expected)
        {
            Assert.True(actual.ContainsKey(key), $"Missing data key '{key}'");
            Assert.Equal(value, actual[key]);
        }
    }
}
