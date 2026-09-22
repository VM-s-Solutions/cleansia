using System.Reflection;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Tenancy;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D3 (Verification #2) — the customer-audience roster, pinned whole. Every money or
/// entitlement act carries its frozen label, resource type and anonymous-actor flag, and NOTHING else
/// carries the customer audience: a marker added, dropped or relabelled anywhere in
/// <c>Cleansia.Core.AppServices</c> reddens this, the same discipline as the erasure roster. The
/// self-export is on it by owner ruling (Q-AUD-O3): a whole-record egress is recorded whoever asks; the
/// session acts are on it by owner ruling too (Q-AUD-L5 overruled): a sign-in, a sign-out, a password
/// reset and an e-mail confirmation are a login history kept beyond the refresh-token window. The one
/// admin label a customer marker carries is pinned here too (the sign-out an administrator runs is an
/// admin act, owner ruling 2026-09-15). The absences are asserted by name because each is a decision,
/// not an omission.
/// </summary>
public sealed class CustomerAuditActionRosterTests
{
    private sealed record Expected(string Label, string ResourceType, bool AllowsAnonymousActor = false, string? AdminLabel = null);

    private static readonly IReadOnlyDictionary<Type, Expected> Roster = new Dictionary<Type, Expected>
    {
        [typeof(CancelOrder.Command)] = new("customer.order.cancel", "Order"),
        [typeof(CancelGuestOrder.Command)] = new("customer.order.cancel", "Order", AllowsAnonymousActor: true),
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
        [typeof(Login.Command)] = new("customer.session.login", "User", AllowsAnonymousActor: true),
        [typeof(MobileLogin.Command)] = new("customer.session.login", "User", AllowsAnonymousActor: true),
        [typeof(GoogleAuth.Command)] = new("customer.session.login", "User", AllowsAnonymousActor: true),
        [typeof(AppleAuth.Command)] = new("customer.session.login", "User", AllowsAnonymousActor: true),
        [typeof(Logout.Command)] = new("customer.session.logout", "User", AdminLabel: "admin.session.logout"),
        [typeof(RequestPasswordChange.Command)] = new("customer.password.reset_requested", "User", AllowsAnonymousActor: true),
        [typeof(ChangePassword.Command)] = new("customer.password.reset_completed", "User", AllowsAnonymousActor: true),
        [typeof(ConfirmUserEmail.Command)] = new("customer.account.email_confirmed", "User", AllowsAnonymousActor: true),
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
    public void The_Customer_Roster_Has_Exactly_The_Declared_Commands_With_Their_Frozen_Labels()
    {
        var marked = MarkedInProduction();

        var actual = marked.ToDictionary(
            x => x.Command,
            x => new Expected(x.Marker.Action!, x.Marker.ResourceType!, x.Marker.AllowsAnonymousActor, x.Marker.AdminAction));

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
            Assert.Equal(marker.AdminAction ?? marker.Action, descriptor.AdminAction);
            Assert.Equal(AuditAudience.Customer, descriptor.Audience);
            Assert.True(descriptor.Audited);
            Assert.False(descriptor.Sensitive, $"{feature.Name}: a customer row has no sensitive tier");
        }
    }

    [Fact]
    public void TwentyOne_Distinct_Labels_Because_Both_Subscribe_Surfaces_Share_One_And_Four_SignIns_Share_One()
    {
        var labels = MarkedInProduction().Select(x => x.Marker.Action).Distinct().ToList();

        Assert.Equal(21, labels.Count);
        Assert.All(labels, l => Assert.StartsWith("customer.", l));
    }

    /// <summary>
    /// The admin arm keeps a customer marker's label unless the marker names an admin one, and only the
    /// sign-out does: it is the one customer-marked command the admin host dispatches, and an
    /// administrator's sign-out is an admin act. An admin label is an admin label.
    /// </summary>
    [Fact]
    public void Only_The_SignOut_Carries_An_Admin_Label_And_It_Is_An_Admin_One()
    {
        var withAdminLabel = MarkedInProduction().Where(x => x.Marker.AdminAction is not null).ToList();

        var (feature, _, marker) = Assert.Single(withAdminLabel);
        Assert.Equal(typeof(Logout), feature);
        Assert.Equal("admin.session.logout", marker.AdminAction);
        Assert.StartsWith("admin.", marker.AdminAction);
    }

    /// <summary>
    /// An anonymous actor is recorded only where the act genuinely has no session yet: registration,
    /// guest checkout, and the session acts that open or recover one. A signed-in act (a sign-out) is not
    /// on the list, so a system job can never be recorded as a guest sign-out.
    /// </summary>
    [Fact]
    public void Only_The_Acts_With_No_Session_Yet_Record_An_Anonymous_Actor()
    {
        var anonymous = MarkedInProduction()
            .Where(x => x.Marker.AllowsAnonymousActor)
            .Select(x => x.Command)
            .ToHashSet();

        Assert.Equal(
            new HashSet<Type>
            {
                typeof(Register.Command),
            typeof(CancelGuestOrder.Command),
            typeof(CreateOrder.Command),
                typeof(Login.Command), typeof(MobileLogin.Command), typeof(GoogleAuth.Command), typeof(AppleAuth.Command),
                typeof(RequestPasswordChange.Command), typeof(ChangePassword.Command), typeof(ConfirmUserEmail.Command),
            },
            anonymous);
    }

    /// <summary>
    /// An anonymous refusal is written out of band and stamped from the ambient tenant, which on an
    /// anonymous request only the scope behaviour sets — from the market the command names, or the
    /// default market when it names none (an explicit <c>CountryId => null</c>). A guest-allowed marker
    /// on a command outside that scope would drop every refusal row with one warning (ADR-0062 D1/D7).
    /// Every marker in the assembly, whichever audience: the admin sign-in is anonymous too.
    /// </summary>
    [Fact]
    public void Every_Guest_Allowed_Marker_Sits_On_An_Operator_Scoped_Command()
    {
        var anonymous = typeof(IAuditContext).Assembly
            .GetTypes()
            .Select(t => (Feature: t, Marker: t.GetCustomAttribute<AuditActionAttribute>(inherit: false)))
            .Where(x => x.Marker is { AllowsAnonymousActor: true })
            .Select(x => (x.Feature, Command: x.Feature.GetNestedType("Command")!))
            .ToList();

        Assert.NotEmpty(anonymous);
        Assert.Contains(anonymous, x => x.Feature == typeof(AdminLogin));
        Assert.All(anonymous, x => Assert.True(
            typeof(IOperatorScopedRequest).IsAssignableFrom(x.Command),
            $"{x.Feature.Name}.Command allows an anonymous actor but is not IOperatorScopedRequest: its refusal rows would have no tenant"));
    }

    /// <summary>
    /// The sign-in-or-register commands are marked as the SIGN-IN; the handler declines the row on the
    /// provisioning branch (<c>IAuditContext.DeclineSuccessRow</c>), whose proof is the consent rows.
    /// </summary>
    [Theory]
    [InlineData(typeof(GoogleAuth))]
    [InlineData(typeof(AppleAuth))]
    public void The_Social_SignIn_Or_Register_Commands_Are_Marked_As_The_SignIn(Type feature)
    {
        var descriptor = AuditActionDescriptor.For(feature.GetNestedType("Command")!);

        Assert.Equal("customer.session.login", descriptor.Action);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.True(descriptor.AllowsAnonymousActor);
    }

    /// <summary>The token row is the record of a refresh; a row per refresh would be volume without a claim.</summary>
    [Fact]
    public void The_Token_Refresh_Is_Not_Marked()
    {
        Assert.Null(typeof(RefreshToken).GetCustomAttribute<AuditActionAttribute>(inherit: false));
    }

    /// <summary>The employee table is not for sessions: a cleaner's own sign-in leaves no row anywhere.</summary>
    [Theory]
    [InlineData(typeof(PartnerLogin))]
    [InlineData(typeof(MobilePartnerLogin))]
    public void The_Partner_SignIns_Are_Not_Marked(Type feature)
    {
        Assert.Null(feature.GetCustomAttribute<AuditActionAttribute>(inherit: false));
    }

    /// <summary>An administrator's sign-in is an admin act, not a customer one; its label is pinned in <c>AdminSessionAuditLabelTests</c>.</summary>
    [Fact]
    public void The_Admin_SignIn_Is_Marked_For_The_Admin_Audience_Not_The_Customer_One()
    {
        var marker = typeof(AdminLogin).GetCustomAttribute<AuditActionAttribute>(inherit: false);

        Assert.NotNull(marker);
        Assert.Equal(AuditAudience.Admin, marker.Audience);
        Assert.DoesNotContain(typeof(AdminLogin.Command), MarkedInProduction().Select(x => x.Command));
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
