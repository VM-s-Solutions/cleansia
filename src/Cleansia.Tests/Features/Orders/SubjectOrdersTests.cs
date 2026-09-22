using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-15 (ADR-0062 D5, A7 by e-mail): the orders that are a subject's, for the erasure
/// and for the export alike — the ones booked on the account, and the GUEST bookings placed with the
/// account's e-mail address. One predicate, so what is erased is what is exported.
///
/// <para>The negative cases are the point. Without the <c>UserId == null</c> term the e-mail match would
/// reach another ACCOUNT's order that merely carries the subject's address in its contact field, and
/// anonymise a stranger's booking underneath them; without the case fold a guest who typed their address
/// in capitals at checkout would keep their name, phone and address on file after asking to be
/// forgotten.</para>
/// </summary>
public class SubjectOrdersTests
{
    private const string SubjectId = "user-subject-1";
    private const string SubjectEmail = "Zdenka.Hruskova@cleansia.test";
    private const string StrangerId = "user-stranger-1";

    [Fact]
    public void An_Order_Booked_On_The_Account_Is_The_Subjects_Whatever_Its_Contact_Email()
    {
        var order = NewOrder(userId: SubjectId, customerEmail: "another.address@cleansia.test");

        Assert.True(Matches(order));
    }

    [Fact]
    public void A_Guest_Order_Placed_With_The_Subjects_Email_Is_The_Subjects_Whatever_The_Case_It_Was_Typed_In()
    {
        Assert.True(Matches(NewOrder(userId: null, customerEmail: "ZDENKA.HRUSKOVA@CLEANSIA.TEST")));
        Assert.True(Matches(NewOrder(userId: null, customerEmail: "zdenka.hruskova@cleansia.test")));
    }

    [Fact]
    public void A_Guest_Order_Placed_With_Another_Email_Is_Not_The_Subjects()
    {
        Assert.False(Matches(NewOrder(userId: null, customerEmail: "tomas.svoboda@cleansia.test")));
    }

    [Fact]
    public void Another_Accounts_Order_Carrying_The_Subjects_Email_Is_Not_The_Subjects()
    {
        Assert.False(Matches(NewOrder(userId: StrangerId, customerEmail: SubjectEmail)));
    }

    private static bool Matches(Order order) => SubjectOrders.Of(SubjectId, SubjectEmail).Compile()(order);

    private static Order NewOrder(string? userId, string customerEmail) =>
        Order.Create(
            customerName: "Zdenka Hruskova",
            customerEmail: customerEmail,
            customerPhone: "+420777222333",
            customerAddress: Address.Create("Erasure St 1", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Cash,
            totalPrice: 1500m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: userId);
}
