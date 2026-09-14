using System.Reflection;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D3 (Verification #2) — the customer-audience roster, pinned whole. Every money or
/// entitlement act carries its frozen label, resource type and anonymous-actor flag, and NOTHING else
/// carries the customer audience: a marker added, dropped or relabelled anywhere in
/// <c>Cleansia.Core.AppServices</c> reddens this, the same discipline as the erasure roster. The
/// self-export is on it by owner ruling (Q-AUD-O3): a whole-record egress is recorded whoever asks. The
/// three absences are asserted by name because each is a decision, not an omission.
/// </summary>
public sealed class CustomerAuditActionRosterTests
{
    private sealed record Expected(string Label, string ResourceType, bool AllowsAnonymousActor = false);

    private static readonly IReadOnlyDictionary<Type, Expected> Roster = new Dictionary<Type, Expected>
    {
        [typeof(CancelOrder.Command)] = new("customer.order.cancel", "Order"),
        [typeof(CreateOrder.Command)] = new("customer.order.create", "Order", AllowsAnonymousActor: true),
        [typeof(ConfirmRecurringOrder.Command)] = new("customer.order.recurring.confirm", "Order"),
        [typeof(CreateDispute.Command)] = new("customer.dispute.create", "Order"),
        [typeof(Register.Command)] = new("customer.account.register", "User", AllowsAnonymousActor: true),
        [typeof(GrantConsent.Command)] = new("customer.consent.grant", "User"),
        [typeof(WithdrawConsent.Command)] = new("customer.consent.withdraw", "User"),
        [typeof(ExportUserData.Command)] = new("customer.gdpr.export", "User"),
        [typeof(CreateMembershipCheckoutSession.Command)] = new("customer.membership.subscribe", "UserMembership"),
        [typeof(CreateMembershipSubscription.Command)] = new("customer.membership.subscribe", "UserMembership"),
        [typeof(SwapMembershipPlan.Command)] = new("customer.membership.swap", "UserMembership"),
        [typeof(CancelMembershipSubscription.Command)] = new("customer.membership.cancel", "UserMembership"),
        [typeof(UpdateNotificationPreferences.Command)] = new("customer.notification_preferences.update", "User"),
        [typeof(CreateRecurringBooking.Command)] = new("customer.recurring.create", "RecurringBookingTemplate"),
        [typeof(UpdateRecurringBooking.Command)] = new("customer.recurring.update", "RecurringBookingTemplate"),
        [typeof(SetRecurringBookingActive.Command)] = new("customer.recurring.set_active", "RecurringBookingTemplate"),
        [typeof(DeleteRecurringBooking.Command)] = new("customer.recurring.delete", "RecurringBookingTemplate"),
    };

    /// <summary>The marker sits on the outer feature class; the command the pipeline sees is its nested <c>Command</c>.</summary>
    private static IReadOnlyList<(Type Feature, Type Command, AuditActionAttribute Marker)> MarkedInProduction() =>
        typeof(IAuditContext).Assembly
            .GetTypes()
            .Select(t => (Feature: t, Marker: t.GetCustomAttribute<AuditActionAttribute>(inherit: false)))
            .Where(x => x.Marker is { Audience: AuditAudience.Customer })
            .Select(x => (x.Feature, Command: x.Feature.GetNestedType("Command")!, x.Marker!))
            .ToList();

    [Fact]
    public void The_Customer_Roster_Is_Exactly_The_Seventeen_Commands_With_Their_Frozen_Labels()
    {
        var marked = MarkedInProduction();

        var actual = marked.ToDictionary(
            x => x.Command,
            x => new Expected(x.Marker.Action!, x.Marker.ResourceType!, x.Marker.AllowsAnonymousActor));

        var unexpected = actual.Keys.Except(Roster.Keys).Select(t => t.FullName).OrderBy(x => x).ToList();
        var missing = Roster.Keys.Except(actual.Keys).Select(t => t.FullName).OrderBy(x => x).ToList();
        Assert.True(unexpected.Count == 0 && missing.Count == 0,
            "ADR-0062 D3: the customer-audience roster moved. A new customer act needs its label, resource type "
            + "and evidence record ratified here; a dropped one needs the same.\n  unexpected: "
            + string.Join(", ", unexpected) + "\n  missing: " + string.Join(", ", missing));

        foreach (var (command, expected) in Roster)
        {
            Assert.Equal(expected, actual[command]);
        }
    }

    [Fact]
    public void Every_Marked_Feature_Nests_The_Command_The_Descriptor_Unwraps_To()
    {
        foreach (var (feature, command, marker) in MarkedInProduction())
        {
            Assert.NotNull(command);
            Assert.True(command.Name.EndsWith("Command", StringComparison.Ordinal),
                $"{feature.Name}: the UoW and the gate key on a type name ending in Command");
            var descriptor = AuditActionDescriptor.For(command);
            Assert.Equal(marker.Action, descriptor.Action);
            Assert.Equal(AuditAudience.Customer, descriptor.Audience);
            Assert.True(descriptor.Audited);
            Assert.False(descriptor.Sensitive, $"{feature.Name}: a customer row has no sensitive tier");
        }
    }

    [Fact]
    public void Sixteen_Distinct_Labels_Because_Both_Subscribe_Surfaces_Share_One()
    {
        var labels = MarkedInProduction().Select(x => x.Marker.Action).Distinct().ToList();

        Assert.Equal(16, labels.Count);
        Assert.All(labels, l => Assert.StartsWith("customer.", l));
    }

    [Fact]
    public void Only_Registration_And_Guest_Checkout_Record_An_Anonymous_Actor()
    {
        var anonymous = MarkedInProduction()
            .Where(x => x.Marker.AllowsAnonymousActor)
            .Select(x => x.Command)
            .ToHashSet();

        Assert.Equal(new HashSet<Type> { typeof(Register.Command), typeof(CreateOrder.Command) }, anonymous);
    }

    /// <summary>
    /// Sign-in-or-register commands: a marker would write a registration row on every social login and a
    /// failure row with IP on every bad token — the login history the ADR declines (Q-AUD-L5).
    /// </summary>
    [Theory]
    [InlineData(typeof(GoogleAuth))]
    [InlineData(typeof(AppleAuth))]
    public void The_Social_SignIn_Or_Register_Commands_Are_Not_Marked(Type feature)
    {
        Assert.Null(feature.GetCustomAttribute<AuditActionAttribute>(inherit: false));
        Assert.Equal(AuditAudience.Admin, AuditActionDescriptor.For(feature.GetNestedType("Command")!).Audience);
    }

    /// <summary>
    /// <c>GdprRequest</c> is the erasure's own record, and a row written by the act that pseudonymises the
    /// table would be the one row the pseudonymisation ran before.
    /// </summary>
    [Fact]
    public void The_Erasure_Is_Not_Marked()
    {
        Assert.Null(typeof(DeleteUserAccount).GetCustomAttribute<AuditActionAttribute>(inherit: false));
    }
}
