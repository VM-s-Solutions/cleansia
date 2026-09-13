using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D1 (Verification #1) — the gate has exactly two arms. The admin arm is ADR-0012 D3 verbatim
/// (every admin Command, opt-out); the customer arm is opt-in by marker, takes the Customer role, and
/// takes an anonymous caller only where the marker says so. Pure logic, red-first.
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

    private static AuditAudience? Resolve(object request, UserProfile? role) =>
        AuditGate.Resolve(request, AuditActionDescriptor.For(request.GetType()), Session(role));

    // ── the admin arm, ADR-0012 D3 ─────────────────────────────────────────────

    [Fact]
    public void An_Administrator_Running_An_Unmarked_Command_Is_The_Admin_Audience()
    {
        Assert.Equal(AuditAudience.Admin, Resolve(new UnmarkedCommand("ORD-1"), UserProfile.Administrator));
    }

    [Fact]
    public void An_Administrator_Running_A_Customer_Marked_Command_Still_Lands_In_The_Admin_Table()
    {
        Assert.Equal(AuditAudience.Admin, Resolve(new CustomerMarkedCommand("ORD-1"), UserProfile.Administrator));
    }

    [Fact]
    public void A_Query_Is_Never_Audited_For_Anyone()
    {
        Assert.Null(Resolve(new UnmarkedQuery("ORD-1"), UserProfile.Administrator));
        Assert.Null(Resolve(new UnmarkedQuery("ORD-1"), UserProfile.Customer));
        Assert.Null(Resolve(new UnmarkedQuery("ORD-1"), role: null));
    }

    [Fact]
    public void An_Opted_Out_Marker_Silences_Both_Arms()
    {
        Assert.Null(Resolve(new OptedOutCustomerCommand("ORD-1"), UserProfile.Administrator));
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
        Assert.Null(Resolve(new CustomerMarkedCommand("ORD-1"), UserProfile.Employee));
    }

    [Fact]
    public void An_Anonymous_Caller_Of_A_Marker_Without_AllowsAnonymousActor_Produces_No_Row()
    {
        Assert.Null(Resolve(new CustomerMarkedCommand("ORD-1"), role: null));
    }

    [Fact]
    public void An_Anonymous_Caller_Of_A_Marker_With_AllowsAnonymousActor_Is_The_Customer_Audience()
    {
        Assert.Equal(AuditAudience.Customer, Resolve(new GuestAllowedCommand("ORD-1"), role: null));
    }

    [Fact]
    public void An_Anonymous_Caller_Of_An_Unmarked_Command_Produces_No_Row()
    {
        Assert.Null(Resolve(new UnmarkedCommand("ORD-1"), role: null));
    }

    [Fact]
    public void An_Employee_Running_A_Guest_Allowed_Command_Still_Produces_No_Row()
    {
        Assert.Null(Resolve(new GuestAllowedCommand("ORD-1"), UserProfile.Employee));
    }

    [Fact]
    public void The_Marker_On_The_Declaring_Type_Reaches_A_Nested_Command_Record()
    {
        Assert.Equal(AuditAudience.Customer, Resolve(new Nested.Feature.Command("ORD-1"), UserProfile.Customer));
    }
}
