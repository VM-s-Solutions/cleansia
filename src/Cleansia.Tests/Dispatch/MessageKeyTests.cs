using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// TC-KEY-0 (ADR-0002 verify #6 / AC4) — the deterministic-key property the whole dispatch contract
/// rests on. Every producer-side <c>MessageKey</c> follows the frozen D2.1 formula and is a pure
/// function of its domain inputs: two invocations with the SAME inputs emit the SAME key (no
/// <c>Guid.NewGuid()</c>/timestamp), so a duplicate enqueue (the Stripe-retry hazard) and a
/// redelivery collapse onto one key and the consumer recognizes the effect as already-done.
///
/// Frozen formulas (ADR-0002 D2.1 table):
///   generate-receipt        → receipt:{OrderId}
///   notifications-dispatch   → push:{UserId}:{EventKey}:{OrderId?}
///   calculate-order-pay      → pay:{OrderId}:{EmployeeId}
///   generate-invoice         → invoice:{PayPeriodId}:{EmployeeId}
///
/// These cases pair with T-0127 and merge with the T-0118 fix. Written test-first (RED until
/// <see cref="MessageKeys"/> exists in Cleansia.Core.Queue.Abstractions).
/// </summary>
public class MessageKeyTests
{
    [Fact]
    public void Receipt_Key_Follows_Frozen_Formula()
    {
        Assert.Equal("receipt:ORDER-1", MessageKeys.Receipt("ORDER-1"));
    }

    [Fact]
    public void Receipt_Key_Is_Deterministic_For_Same_Inputs()
    {
        Assert.Equal(MessageKeys.Receipt("ORDER-1"), MessageKeys.Receipt("ORDER-1"));
    }

    [Fact]
    public void Push_Key_Follows_Frozen_Formula_With_Subject()
    {
        Assert.Equal(
            "push:USER-1:order.confirmed:ORDER-1",
            MessageKeys.Push("USER-1", "order.confirmed", "ORDER-1"));
    }

    [Fact]
    public void Push_Key_Omits_Empty_Subject_But_Keeps_Trailing_Separator()
    {
        // The frozen formula is push:{UserId}:{EventKey}:{OrderId?} — the subject segment is optional.
        // A null/empty subject yields the same stable shape (so a subjectless push still dedups per
        // (user,event)).
        Assert.Equal("push:USER-1:dispute.reply:", MessageKeys.Push("USER-1", "dispute.reply", null));
        Assert.Equal(
            MessageKeys.Push("USER-1", "dispute.reply", null),
            MessageKeys.Push("USER-1", "dispute.reply", string.Empty));
    }

    [Fact]
    public void Push_Key_Is_Deterministic_For_Same_Inputs()
    {
        Assert.Equal(
            MessageKeys.Push("USER-1", "order.completed", "ORDER-9"),
            MessageKeys.Push("USER-1", "order.completed", "ORDER-9"));
    }

    [Fact]
    public void Pay_Key_Follows_Frozen_Formula()
    {
        Assert.Equal("pay:ORDER-1:EMP-2", MessageKeys.Pay("ORDER-1", "EMP-2"));
    }

    [Fact]
    public void Pay_Key_Is_Deterministic_For_Same_Inputs()
    {
        Assert.Equal(MessageKeys.Pay("ORDER-1", "EMP-2"), MessageKeys.Pay("ORDER-1", "EMP-2"));
    }

    [Fact]
    public void Invoice_Key_Follows_Frozen_Formula()
    {
        Assert.Equal("invoice:PERIOD-1:EMP-2", MessageKeys.Invoice("PERIOD-1", "EMP-2"));
    }
}
