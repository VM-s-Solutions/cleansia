using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0036 D5 — the five terms of the preferred-cleaner hold, in the in-memory form. The queryable
/// form is pinned against this one over real Postgres by <c>PreferredHoldVisibilityAgreementTests</c>
/// (TC-PREF-EQUIV-0); sharing an expression tree would not have made the two agree, because SQL and C#
/// disagree on null equality.
///
/// <para>Two of the five terms exist only to make a stuck-held order unreachable, and each of them has
/// its own case below: without the null-beneficiary disjunct a row carrying a deadline and no
/// beneficiary is invisible and un-takeable to EVERY cleaner for up to 12 hours with no actor permitted
/// to clear it, and without the any-assignment disjunct a seat stays locked after the perk has already
/// been delivered.</para>
/// </summary>
public class OrderVisibilityTests
{
    private const string Beneficiary = "employee-preferred";
    private const string Bystander = "employee-other";

    private static readonly DateTime Now = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_Order_With_No_Deadline_Is_Open_To_Everyone()
    {
        var order = NewOrder(preferredEmployeeId: null, holdUntilUtc: null);

        Assert.True(OrderVisibility.NotHeldFrom(order, Bystander, Now));
        Assert.True(OrderVisibility.NotHeldFrom(order, Beneficiary, Now));
        Assert.True(OrderVisibility.NotHeldFrom(order, null, Now));
    }

    /// <summary>
    /// The stranded pair: a live deadline with nobody able to act on it. <c>GrantPreferredHold</c>
    /// refuses to write it, but a manual UPDATE or a future writer could, and the rule has to fail OPEN
    /// rather than take the order off the board for everyone.
    /// </summary>
    [Fact]
    public void A_Live_Deadline_With_No_Beneficiary_Is_Open_To_Everyone()
    {
        var order = NewOrder(preferredEmployeeId: null, holdUntilUtc: Now.AddHours(2));

        Assert.True(OrderVisibility.NotHeldFrom(order, Bystander, Now));
        Assert.True(OrderVisibility.NotHeldFrom(order, Beneficiary, Now));
        Assert.True(OrderVisibility.NotHeldFrom(order, null, Now));
    }

    [Fact]
    public void A_Live_Hold_Closes_The_Order_To_Every_Other_Cleaner()
    {
        var order = NewOrder(Beneficiary, Now.AddHours(2));

        Assert.False(OrderVisibility.NotHeldFrom(order, Bystander, Now));
    }

    [Fact]
    public void A_Live_Hold_Leaves_The_Order_Open_To_Its_Beneficiary()
    {
        var order = NewOrder(Beneficiary, Now.AddHours(2));

        Assert.True(OrderVisibility.NotHeldFrom(order, Beneficiary, Now));
    }

    /// <summary>
    /// A caller with no employee id is never the beneficiary. C# says <c>null == null</c> is true and
    /// SQL says <c>col = NULL</c> is unknown; the in-memory form guards it so the two agree whatever
    /// order the terms are written in.
    /// </summary>
    [Fact]
    public void A_Caller_With_No_Employee_Id_Is_Never_The_Beneficiary()
    {
        var order = NewOrder(Beneficiary, Now.AddHours(2));

        Assert.False(OrderVisibility.NotHeldFrom(order, null, Now));
        Assert.False(OrderVisibility.NotHeldFrom(order, string.Empty, Now));
    }

    [Fact]
    public void The_Hold_Expires_By_Clock_With_No_Actor()
    {
        var order = NewOrder(Beneficiary, Now.AddHours(2));

        Assert.False(OrderVisibility.NotHeldFrom(order, Bystander, Now));
        Assert.True(OrderVisibility.NotHeldFrom(order, Bystander, Now.AddHours(2)));
        Assert.True(OrderVisibility.NotHeldFrom(order, Bystander, Now.AddHours(3)));
    }

    /// <summary>
    /// Invariant H per SEAT: the hold is first refusal on the FIRST assignment, not a lease on the
    /// order. Without this term the beneficiary takes seat 1 and the remaining seats stay locked to
    /// everyone — including to the beneficiary, whom TakeOrder refuses a second seat — for the rest of
    /// the window, consuming 100% of those seats' fill window for a perk already honoured.
    /// </summary>
    [Fact]
    public void A_Hold_Is_Consumed_The_Moment_The_Order_Has_Any_Cleaner()
    {
        var takenByBeneficiary = NewOrder(Beneficiary, Now.AddHours(2), assignedEmployeeId: Beneficiary);
        var takenByAdminAssignment = NewOrder(Beneficiary, Now.AddHours(2), assignedEmployeeId: Bystander);

        Assert.True(OrderVisibility.NotHeldFrom(takenByBeneficiary, Bystander, Now));
        Assert.True(OrderVisibility.NotHeldFrom(takenByAdminAssignment, Bystander, Now));
    }

    private static Order NewOrder(
        string? preferredEmployeeId,
        DateTime? holdUntilUtc,
        string? assignedEmployeeId = null)
    {
        var order = Order.Create(
            customerName: "Hold Customer",
            customerEmail: "hold@cleansia.test",
            customerPhone: "+420777444555",
            customerAddress: Address.Create("Hold St 1", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: Now.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            preferredEmployeeId: preferredEmployeeId);

        if (holdUntilUtc is not null)
        {
            // The aggregate refuses a deadline with no beneficiary, so the stranded pair is reached the
            // only way it can be reached in production — a write that bypassed GrantPreferredHold.
            if (preferredEmployeeId is null)
            {
                typeof(Order)
                    .GetProperty(nameof(Order.PreferredHoldUntilUtc))!
                    .GetSetMethod(nonPublic: true)!
                    .Invoke(order, [holdUntilUtc]);
            }
            else
            {
                order.GrantPreferredHold(preferredEmployeeId, holdUntilUtc.Value);
            }
        }

        if (assignedEmployeeId is not null)
        {
            var user = User.CreateWithPassword(
                $"{assignedEmployeeId}@cleansia.test", "Test-password-1!", "Ida", "Assigned", UserProfile.Employee);
            var employee = Employee.CreateWithUser(user);
            employee.Id = assignedEmployeeId;
            order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        }

        return order;
    }
}
