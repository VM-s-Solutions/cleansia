using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Orders;

/// <summary>
/// ADR-0045 D5.1 — <c>GrantPreferredHold</c> widened from "set once, at creation" to re-callable. The
/// aggregate keeps the STRUCTURAL invariants; the policy numbers are passed in, the same way
/// <c>CalculateRequiredEmployees</c> takes <c>SpareSeatsPerOrder</c> and <c>Cancel</c> takes an
/// already-computed fee rate.
///
/// <para>The invariant that can actually fail is <b>no live reservation for someone else</b>, and it
/// keys on the HOLD rather than on the preference column: <c>Order.Create</c> writes
/// <c>PreferredEmployeeId</c> independently of any hold, so an invariant phrased on the preference
/// would refuse re-offers that never held anything and permit ones that do — backwards on both
/// sides.</para>
///
/// <para>The drafted guard was <i>"untilUtc must exceed the stored value"</i> and it was VACUOUS:
/// <c>untilUtc(t) = t + min(0.10(L - t), 12)</c> is strictly increasing in t, so at L = 24 h a re-grant
/// three minutes after booking yields 2.445 h against a stored 2.4 h and passes. It refused nothing in
/// the grantable domain and would have permitted exactly the silent eviction it named.</para>
/// </summary>
public class PreferredOfferGrantTests
{
    private static readonly DateTime Now = new(2026, 8, 9, 9, 0, 0, DateTimeKind.Utc);
    private const string First = "employee-first";
    private const string Second = "employee-second";
    private const int MaxRounds = 2;

    [Fact]
    public void The_First_Grant_Is_Round_One()
    {
        var order = NewOrder();

        order.GrantPreferredHold(First, Now.AddHours(2), Now, MaxRounds);

        Assert.Equal(First, order.PreferredEmployeeId);
        Assert.Equal(Now.AddHours(2), order.PreferredHoldUntilUtc);
        Assert.Equal(1, order.PreferredOfferRound);
    }

    [Fact]
    public void A_Re_Offer_After_The_Lapse_Is_Round_Two()
    {
        var order = NewOrder();
        order.GrantPreferredHold(First, Now.AddHours(2), Now, MaxRounds);

        var afterLapse = Now.AddHours(2);
        order.GrantPreferredHold(Second, afterLapse.AddHours(1), afterLapse, MaxRounds);

        Assert.Equal(Second, order.PreferredEmployeeId);
        Assert.Equal(2, order.PreferredOfferRound);
    }

    /// <summary>
    /// The counter has ONE writer and increments once per grant, so <c>Round &lt; Max</c> admits exactly
    /// two reservations. A second writer, or a grant that did not increment, would admit three (0 to 1
    /// to 2, refused only on the fourth call) and Invariant H's arithmetic would be wrong.
    /// </summary>
    [Fact]
    public void The_Round_After_The_Cap_Is_Refused()
    {
        var order = NewOrder();
        var cursor = Now;
        for (var round = 0; round < MaxRounds; round++)
        {
            order.GrantPreferredHold(First, cursor.AddHours(1), cursor, MaxRounds);
            cursor = cursor.AddHours(1);
        }

        Assert.Throws<InvalidOperationException>(
            () => order.GrantPreferredHold(Second, cursor.AddHours(1), cursor, MaxRounds));
        Assert.Equal(MaxRounds, order.PreferredOfferRound);
    }

    [Fact]
    public void A_Live_Beneficiary_Cannot_Be_Evicted()
    {
        var order = NewOrder();
        order.GrantPreferredHold(First, Now.AddHours(2), Now, MaxRounds);

        Assert.Throws<InvalidOperationException>(
            () => order.GrantPreferredHold(Second, Now.AddHours(3), Now.AddMinutes(3), MaxRounds));
        Assert.Equal(First, order.PreferredEmployeeId);
        Assert.Equal(1, order.PreferredOfferRound);
    }

    /// <summary>
    /// The refutation of the vacuous guard, as a test: the monotone quantity really does increase, so a
    /// "later deadline than the stored one" rule would have let this through.
    /// </summary>
    [Fact]
    public void The_Eviction_Attempt_Refused_Above_Carries_A_LATER_Deadline_Than_The_One_It_Would_Replace()
    {
        var cleaning = Now.AddHours(24);
        var atBooking = BookingPolicyHold(cleaning, Now);
        var threeMinutesLater = BookingPolicyHold(cleaning, Now.AddMinutes(3));

        Assert.True(
            Now.AddMinutes(3).Add(threeMinutesLater) > Now.Add(atBooking),
            "the monotonicity that made the drafted guard vacuous no longer holds; re-read D5.1");
    }

    [Fact]
    public void A_Grant_Whose_Deadline_Is_Not_In_The_Future_Is_Refused()
    {
        var order = NewOrder();

        Assert.Throws<ArgumentException>(() => order.GrantPreferredHold(First, Now, Now, MaxRounds));
        Assert.Equal(0, order.PreferredOfferRound);
    }

    [Fact]
    public void A_Grant_With_No_Beneficiary_Is_Refused()
    {
        var order = NewOrder();

        Assert.Throws<ArgumentException>(
            () => order.GrantPreferredHold("  ", Now.AddHours(2), Now, MaxRounds));
        Assert.Null(order.PreferredHoldUntilUtc);
    }

    /// <summary>A taken order has no reservation to grant — term 5 of the visibility rule already
    /// released its seats to the whole board.</summary>
    [Fact]
    public void An_Order_That_Already_Has_A_Cleaner_Cannot_Be_Reserved()
    {
        var order = NewOrder();
        order.AddAssignedEmployee(OrderEmployee.Create(order, NewEmployee()));

        Assert.Throws<InvalidOperationException>(
            () => order.GrantPreferredHold(First, Now.AddHours(2), Now, MaxRounds));
    }

    /// <summary>
    /// The lapse receipt is per RESERVATION, not per order. Without this the sweep's
    /// <c>PreferredOfferLapseNotifiedAt == null</c> idempotency term would suppress round two's message
    /// entirely, and AC4's "never zero" would be false for every customer who used their re-offer.
    /// </summary>
    [Fact]
    public void A_New_Reservation_Clears_The_Previous_Ones_Lapse_Receipt()
    {
        var order = NewOrder();
        order.GrantPreferredHold(First, Now.AddHours(2), Now, MaxRounds);
        var afterLapse = Now.AddHours(2);
        order.MarkPreferredOfferLapseNotified(afterLapse);

        order.GrantPreferredHold(Second, afterLapse.AddHours(1), afterLapse, MaxRounds);

        Assert.Null(order.PreferredOfferLapseNotifiedAt);
    }

    [Fact]
    public void Ending_A_Hold_Keeps_The_Beneficiary_On_The_Row()
    {
        var order = NewOrder();
        order.GrantPreferredHold(First, Now.AddHours(2), Now, MaxRounds);

        order.EndPreferredHold(Now.AddMinutes(10));

        Assert.Equal(Now.AddMinutes(10), order.PreferredHoldUntilUtc);
        Assert.Equal(First, order.PreferredEmployeeId);
    }

    /// <summary>
    /// A second decline, or a sweep racing the decline, must not push the deadline FORWARD and re-open
    /// a reservation the clock already closed.
    /// </summary>
    [Fact]
    public void Ending_A_Hold_Twice_Keeps_The_Earlier_Instant()
    {
        var order = NewOrder();
        order.GrantPreferredHold(First, Now.AddHours(2), Now, MaxRounds);

        order.EndPreferredHold(Now.AddMinutes(10));
        order.EndPreferredHold(Now.AddMinutes(40));

        Assert.Equal(Now.AddMinutes(10), order.PreferredHoldUntilUtc);
    }

    [Fact]
    public void Ending_A_Hold_That_Was_Never_Granted_Writes_Nothing()
    {
        var order = NewOrder();

        order.EndPreferredHold(Now);

        Assert.Null(order.PreferredHoldUntilUtc);
    }

    [Fact]
    public void The_Lapse_Receipt_Keeps_Its_First_Stamp()
    {
        var order = NewOrder();
        order.GrantPreferredHold(First, Now.AddHours(2), Now, MaxRounds);

        order.MarkPreferredOfferLapseNotified(Now.AddHours(2));
        order.MarkPreferredOfferLapseNotified(Now.AddHours(3));

        Assert.Equal(Now.AddHours(2), order.PreferredOfferLapseNotifiedAt);
    }

    private static Order NewOrder()
    {
        var order = Order.Create(
            customerName: "Grant Customer",
            customerEmail: "grant@cleansia.test",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Grant St 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: Now.AddHours(24),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-grant");
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    private static Employee NewEmployee()
    {
        var user = User.CreateWithPassword(
            "cleaner-grant@cleansia.test", "Passw0rd!x", "Grant", "Cleaner", UserProfile.Employee);
        user.Id = "user-cleaner-grant";
        var employee = Employee.CreateWithUser(user);
        employee.Id = "employee-cleaner-grant";
        return employee;
    }

    private static TimeSpan BookingPolicyHold(DateTime cleaningUtc, DateTime nowUtc)
    {
        var leadHours = (cleaningUtc - nowUtc).TotalHours;
        return leadHours < 8 ? TimeSpan.Zero : TimeSpan.FromHours(Math.Min(leadHours * 0.10, 12));
    }
}
