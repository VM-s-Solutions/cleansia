using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.TestUtilities.MockDataFactories.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The order detail now says WHY the platform cancelled a booking — and only when the platform is
/// what cancelled it.
///
/// <para><b>The gate is a privacy boundary, not a filter.</b> <c>Order.CancellationReason</c> is one
/// column serving two writers: the system sweeps put a localisable KEY in it, and a human cancelling
/// puts free text there — text an admin wrote for other staff. Exposing the column unconditionally
/// would ship an internal note to the customer whose order it is. So the mapper reads
/// <c>CancelledBy</c> first, and these cases pin that it does.</para>
///
/// <para>The defect that prompted the field: a card booking whose payment sheet was cancelled
/// auto-cancels later, and the push tells the customer to tap to see why. The detail then said
/// nothing at all, so the reason had to be inferred from the absence of a charge.</para>
/// </summary>
public class SystemCancellationReasonTests
{
    private static Order CancelledBy(CancelledBy actor, string? reason)
    {
        var order = OrderMockFactory.Generate();
        order.Cancel(DateTime.UtcNow, actor, feeRate: 0m, refundAmount: 0m, reason: reason);
        return order;
    }

    [Fact]
    public void A_System_Cancellation_Tells_The_Customer_Why()
    {
        var order = CancelledBy(
            Core.Domain.Enums.CancelledBy.System, OrderCancellationReasons.PaymentNotCompleted);

        Assert.Equal(
            OrderCancellationReasons.PaymentNotCompleted,
            order.MapToDetail(isCustomerCaller: true).SystemCancellationReason);
    }

    /// <summary>
    /// The one that matters. An admin's note is written for staff and must not reach the customer
    /// through a field whose whole purpose is to be rendered to them.
    /// </summary>
    [Fact]
    public void An_Admins_Free_Text_Note_Never_Reaches_The_Customer()
    {
        var order = CancelledBy(
            Core.Domain.Enums.CancelledBy.Admin,
            "customer has three chargebacks, do not rebook — see the fraud thread");

        Assert.Null(order.MapToDetail(isCustomerCaller: true).SystemCancellationReason);
    }

    /// <summary>
    /// The gate is on the ACTOR, not on the caller — a partner or admin reading the same order must
    /// not see the note either, because the field is not where staff notes live.
    /// </summary>
    [Fact]
    public void The_Gate_Holds_For_Every_Caller_Not_Just_The_Customer()
    {
        var order = CancelledBy(Core.Domain.Enums.CancelledBy.Admin, "internal note");

        Assert.Null(order.MapToDetail(isCustomerCaller: false).SystemCancellationReason);
    }

    [Theory]
    [InlineData(Core.Domain.Enums.CancelledBy.Customer)]
    [InlineData(Core.Domain.Enums.CancelledBy.Cleaner)]
    public void A_Human_Cancellation_Carries_No_System_Reason(CancelledBy actor)
    {
        var order = CancelledBy(actor, reason: null);

        Assert.Null(order.MapToDetail().SystemCancellationReason);
    }

    /// <summary>
    /// An order nobody cancelled has nothing to say, and must not render an empty reason row.
    /// </summary>
    [Fact]
    public void A_Live_Order_Has_No_Reason_At_All()
    {
        Assert.Null(OrderMockFactory.Generate().MapToDetail().SystemCancellationReason);
    }

    /// <summary>
    /// Both sweeps write keys from the same closed set, so a client that can translate one can
    /// translate the other — and neither is a sentence that would ship untranslated.
    /// </summary>
    [Fact]
    public void Every_System_Reason_Is_A_Dotted_Key_Not_A_Sentence()
    {
        foreach (var reason in new[]
        {
            OrderCancellationReasons.PaymentNotCompleted,
            OrderCancellationReasons.RecurringNotConfirmed,
        })
        {
            Assert.StartsWith("order.cancelled.", reason, StringComparison.Ordinal);
            Assert.DoesNotContain(' ', reason);
        }
    }
}
