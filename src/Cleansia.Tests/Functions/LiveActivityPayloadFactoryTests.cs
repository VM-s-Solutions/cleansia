using System.Text.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0029 D2/D4 — the pure content-state / stale-date / dismissal-date builder. TC-LA-0 (payload per
/// event), TC-LA-1 (stale-date rule), TC-LA-6 (the S6 allowlist over the builder), + the shared JSON
/// content-state fixture the iOS decoding test (LA-5) also asserts.
/// </summary>
public class LiveActivityPayloadFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ScheduledStart = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ScheduledEnd = new(2026, 7, 20, 11, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static SendLiveActivityUpdateMessage Message(string eventKey, DateTimeOffset? scheduledEnd = null) =>
        new(
            UserId: "USER-1",
            OrderId: "ORDER-1",
            EventKey: eventKey,
            OrderNumber: "ORD-AB12CD34",
            ScheduledStart: ScheduledStart,
            ScheduledEnd: scheduledEnd ?? ScheduledEnd,
            TransitionAtUtc: new DateTimeOffset(2026, 7, 20, 9, 30, 0, TimeSpan.Zero),
            TenantId: null);

    // ── TC-LA-0 — payload per event ─────────────────────────────────────────────────

    [Fact]
    public void Start_Carries_Attributes_And_OnTheWay_State_No_Dismissal()
    {
        var push = LiveActivityPayloadFactory.Build(Message(LiveActivityEventKeys.Start), currentStatus: null, Now);

        Assert.Equal(LiveActivityEventKeys.Start, push.Event);
        Assert.Equal("onTheWay", push.ContentState.Status);
        Assert.Equal("CleanOrderAttributes", push.AttributesType);
        Assert.NotNull(push.Attributes);
        Assert.Equal("ORD-AB12CD34", push.Attributes!.OrderNumber);
        Assert.Null(push.DismissalDate);
    }

    [Fact]
    public void Update_Carries_InProgress_State_And_The_Transition_Timestamp_No_Attributes()
    {
        var message = Message(LiveActivityEventKeys.Update);
        var push = LiveActivityPayloadFactory.Build(message, currentStatus: null, Now);

        Assert.Equal(LiveActivityEventKeys.Update, push.Event);
        Assert.Equal("inProgress", push.ContentState.Status);
        Assert.Equal(message.TransitionAtUtc, push.Timestamp);
        Assert.Null(push.AttributesType);
        Assert.Null(push.Attributes);
        Assert.Null(push.DismissalDate);
    }

    [Fact]
    public void Completed_End_Carries_Completed_State_And_Dismisses_In_30_Minutes()
    {
        var push = LiveActivityPayloadFactory.Build(
            Message(LiveActivityEventKeys.End), currentStatus: OrderStatus.Completed, Now);

        Assert.Equal(LiveActivityEventKeys.End, push.Event);
        Assert.Equal("completed", push.ContentState.Status);
        Assert.Equal(Now.AddMinutes(30), push.DismissalDate);
    }

    [Fact]
    public void Cancelled_End_Carries_Cancelled_State_And_Dismisses_Immediately()
    {
        var push = LiveActivityPayloadFactory.Build(
            Message(LiveActivityEventKeys.End), currentStatus: OrderStatus.Cancelled, Now);

        Assert.Equal(LiveActivityEventKeys.End, push.Event);
        Assert.Equal("cancelled", push.ContentState.Status);
        Assert.Equal(Now, push.DismissalDate);
    }

    [Fact]
    public void End_With_Unknown_Or_Null_Status_Falls_Back_To_Completed()
    {
        var push = LiveActivityPayloadFactory.Build(
            Message(LiveActivityEventKeys.End), currentStatus: null, Now);

        Assert.Equal("completed", push.ContentState.Status);
        Assert.Equal(Now.AddMinutes(30), push.DismissalDate);
    }

    // ── TC-LA-1 — stale-date = max(now + 4h, scheduledEnd + 1h) ──────────────────────

    [Fact]
    public void StaleDate_Uses_The_Now_Plus_4h_Floor_For_A_Short_Or_Past_Clean()
    {
        // scheduledEnd is 1h ago → scheduledEnd + 1h = Now → the now+4h floor wins.
        var push = LiveActivityPayloadFactory.Build(
            Message(LiveActivityEventKeys.Update, scheduledEnd: Now.AddHours(-1)), currentStatus: null, Now);

        Assert.Equal(Now.AddHours(4), push.StaleDate);
    }

    [Fact]
    public void StaleDate_Extends_Past_A_Long_Booked_Clean_So_It_Never_Renders_Stale_MidService()
    {
        // A 6h clean ending at Now+6h → scheduledEnd + 1h = Now+7h beats the now+4h floor.
        var push = LiveActivityPayloadFactory.Build(
            Message(LiveActivityEventKeys.Update, scheduledEnd: Now.AddHours(6)), currentStatus: null, Now);

        Assert.Equal(Now.AddHours(7), push.StaleDate);
    }

    [Fact]
    public void StaleDate_At_The_Boundary_The_Two_Rules_Coincide()
    {
        // scheduledEnd + 1h == now + 4h  ⇔  scheduledEnd == now + 3h.
        var push = LiveActivityPayloadFactory.Build(
            Message(LiveActivityEventKeys.Update, scheduledEnd: Now.AddHours(3)), currentStatus: null, Now);

        Assert.Equal(Now.AddHours(4), push.StaleDate);
    }

    // ── TC-LA-6 — the S6 allowlist, pinned over the builder's OWN output ─────────────

    [Fact]
    public void ContentState_Fields_Are_Exactly_The_S6_Allowlist_Nothing_More()
    {
        foreach (var eventKey in new[] { LiveActivityEventKeys.Start, LiveActivityEventKeys.Update, LiveActivityEventKeys.End })
        {
            var push = LiveActivityPayloadFactory.Build(Message(eventKey), currentStatus: OrderStatus.Completed, Now);
            var json = JsonSerializer.SerializeToElement(push.ContentState, CamelCase);

            var keys = json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
            Assert.Equal(new[] { "orderNumber", "scheduledEnd", "scheduledStart", "status", "v" }, keys);
        }
    }

    // ── the shared cross-platform content-state fixture (LA-5 iOS asserts the SAME bytes) ──

    [Fact]
    public void ContentState_Serializes_To_The_Shared_Wire_Fixture()
    {
        var push = LiveActivityPayloadFactory.Build(Message(LiveActivityEventKeys.Update), currentStatus: null, Now);
        var produced = JsonSerializer.SerializeToElement(push.ContentState, CamelCase);

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Functions", "Fixtures", "live-activity-content-state.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));

        AssertJsonEqual(fixture.RootElement, produced);
    }

    private static void AssertJsonEqual(JsonElement expected, JsonElement actual)
    {
        var expectedProps = expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
        var actualProps = actual.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
        Assert.Equal(expectedProps.OrderBy(k => k.Key), actualProps.OrderBy(k => k.Key));
    }
}
