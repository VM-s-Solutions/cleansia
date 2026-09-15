using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D1 (Verification #1) — the gate has exactly two arms. The admin arm is ADR-0012 D3 verbatim
/// (every admin Command, opt-out); the customer arm is opt-in by marker, takes the Customer role, and
/// takes an anonymous caller only where the marker says so AND the serving host is a customer host: a
/// cleaner's anonymous act on a partner host, and an anonymous act on the admin host, land nowhere.
/// Pure logic, red-first.
/// </summary>
public sealed class AuditGateTests
{
    public sealed record UnmarkedCommand(string OrderId);

    public sealed record UnmarkedQuery(string OrderId);

    [AuditAction("customer.order.cancel", Audience = AuditAudience.Customer, ResourceType = "Order")]
    public sealed record CustomerMarkedCommand(string OrderId);

    [AuditAction("customer.order.create", Audience = AuditAudience.Customer, ResourceType = "Order", AllowsAnonymousActor = true)]
    public sealed record GuestAllowedCommand(string OrderId);

    [AuditAction("customer.something", Audience = AuditAudience.Customer, Audited = false)]
    public sealed record OptedOutCustomerCommand(string OrderId);

    public sealed class Nested
    {
        [AuditAction("customer.nested", Audience = AuditAudience.Customer)]
        public sealed class Feature
        {
            public sealed record Command(string OrderId);
        }
    }

    private static IUserSessionProvider Session(UserProfile? role) =>
        role is null
            ? new TestUserSessionProvider([])
            : new TestUserSessionProvider("user-1", "user@cleansia.test", [new Claim(ClaimTypes.Role, role.Value.ToString())]);

    private static AuditAudience? Resolve(object request, UserProfile? role, string host = JwtAudiences.Customer) =>
        AuditGate.Resolve(request, AuditActionDescriptor.For(request.GetType()), Session(role), new HostAudienceProvider(host));

    // ── the admin arm, ADR-0012 D3 ─────────────────────────────────────────────

    [Fact]
    public void An_Administrator_Running_An_Unmarked_Command_Is_The_Admin_Audience()
    {
        Assert.Equal(AuditAudience.Admin, Resolve(new UnmarkedCommand("ORD-1"), UserProfile.Administrator, JwtAudiences.Admin));
    }

    [Fact]
    public void An_Administrator_Running_A_Customer_Marked_Command_Still_Lands_In_The_Admin_Table()
    {
        Assert.Equal(AuditAudience.Admin, Resolve(new CustomerMarkedCommand("ORD-1"), UserProfile.Administrator, JwtAudiences.Admin));
    }

    [Fact]
    public void A_Query_Is_Never_Audited_For_Anyone()
    {
        Assert.Null(Resolve(new UnmarkedQuery("ORD-1"), UserProfile.Administrator, JwtAudiences.Admin));
        Assert.Null(Resolve(new UnmarkedQuery("ORD-1"), UserProfile.Customer));
        Assert.Null(Resolve(new UnmarkedQuery("ORD-1"), role: null));
    }

    [Fact]
    public void An_Opted_Out_Marker_Silences_Both_Arms()
    {
        Assert.Null(Resolve(new OptedOutCustomerCommand("ORD-1"), UserProfile.Administrator, JwtAudiences.Admin));
        Assert.Null(Resolve(new OptedOutCustomerCommand("ORD-1"), UserProfile.Customer));
    }

    // ── the customer arm, ADR-0062 D1 ──────────────────────────────────────────

    [Fact]
    public void A_Customer_Running_A_Customer_Marked_Command_Is_The_Customer_Audience()
    {
        Assert.Equal(AuditAudience.Customer, Resolve(new CustomerMarkedCommand("ORD-1"), UserProfile.Customer));
    }

    [Fact]
    public void A_Customer_Running_An_Unmarked_Command_Produces_No_Row()
    {
        Assert.Null(Resolve(new UnmarkedCommand("ORD-1"), UserProfile.Customer));
    }

    [Fact]
    public void An_Employee_Running_A_Customer_Marked_Command_Produces_No_Row()
    {
        Assert.Null(Resolve(new CustomerMarkedCommand("ORD-1"), UserProfile.Employee, JwtAudiences.Partner));
    }

    [Fact]
    public void An_Anonymous_Caller_Of_A_Marker_Without_AllowsAnonymousActor_Produces_No_Row()
    {
        Assert.Null(Resolve(new CustomerMarkedCommand("ORD-1"), role: null));
    }

    [Fact]
    public void An_Anonymous_Caller_Of_A_Marker_With_AllowsAnonymousActor_Is_The_Customer_Audience_On_A_Customer_Host()
    {
        Assert.Equal(AuditAudience.Customer, Resolve(new GuestAllowedCommand("ORD-1"), role: null, JwtAudiences.Customer));
    }

    [Fact]
    public void An_Anonymous_Caller_Of_An_Unmarked_Command_Produces_No_Row()
    {
        Assert.Null(Resolve(new UnmarkedCommand("ORD-1"), role: null));
    }

    [Fact]
    public void An_Employee_Running_A_Guest_Allowed_Command_Still_Produces_No_Row()
    {
        Assert.Null(Resolve(new GuestAllowedCommand("ORD-1"), UserProfile.Employee, JwtAudiences.Partner));
    }

    [Fact]
    public void The_Marker_On_The_Declaring_Type_Reaches_A_Nested_Command_Record()
    {
        Assert.Equal(AuditAudience.Customer, Resolve(new Nested.Feature.Command("ORD-1"), UserProfile.Customer));
    }

    // ── the host gate: an anonymous act is a customer act only on a customer host ──

    [Theory]
    [InlineData(JwtAudiences.Partner)]
    [InlineData(JwtAudiences.Mobile)]
    [InlineData(JwtAudiences.Admin)]
    [InlineData("cleansia.functions")]
    public void An_Anonymous_Caller_Of_A_Guest_Allowed_Marker_Lands_Nowhere_Off_The_Customer_Hosts(string host)
    {
        Assert.Null(Resolve(new GuestAllowedCommand("ORD-1"), role: null, host));
    }

    /// <summary>
    /// The case this gate was written for: the partner hosts used to route the anonymous
    /// <c>POST api/Auth/Register</c>, whose command carries the customer marker, and a cleaner registering
    /// there was not a customer act. The route is gone (owner ruling 2026-09-15); the gate still reads
    /// the host, not the marker, so a customer-marked command re-routed on a partner host would land
    /// nowhere rather than in the customer table.
    /// </summary>
    [Fact]
    public void An_Anonymous_Registration_On_The_Partner_Host_Lands_Nowhere()
    {
        var register = new Register.Command("cleaner@cleansia.test", "Secret-123!", "Clean", "Er", "en");

        Assert.Null(Resolve(register, role: null, JwtAudiences.Partner));
        Assert.Equal(AuditAudience.Customer, Resolve(register, role: null, JwtAudiences.Customer));
    }

    [Fact]
    public void An_Anonymous_Login_Is_A_Customer_Act_On_The_Customer_Host_Only()
    {
        var login = new Login.Command("someone@cleansia.test", "Secret-123!", RememberMe: true);

        Assert.Equal(AuditAudience.Customer, Resolve(login, role: null, JwtAudiences.Customer));
        Assert.Null(Resolve(login, role: null, JwtAudiences.Partner));
        Assert.Null(Resolve(login, role: null, JwtAudiences.Admin));
    }

    [Fact]
    public void An_Administrator_On_The_Admin_Host_Running_A_Customer_Marked_Session_Act_Lands_In_The_Admin_Table_Only()
    {
        Assert.Equal(AuditAudience.Admin, Resolve(new Logout.Command("token"), UserProfile.Administrator, JwtAudiences.Admin));
    }
}
