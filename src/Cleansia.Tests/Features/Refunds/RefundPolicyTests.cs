using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// ADR-0009 D1 (14-day soft window on <c>Order.CompletedAt</c>) + D3 (Stripe-fee bearer). Pure rule
/// tests, no infra.
/// </summary>
public class RefundPolicyTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    // TC-REFUND-WINDOW — within 14 days is open.
    [Fact]
    public void WithinFourteenDaysOfCompletion_IsOpen()
    {
        Assert.True(RefundPolicy.IsWithinWindow(Now.AddDays(-13), Now));
        Assert.True(RefundPolicy.IsWithinWindow(Now.AddHours(-1), Now));
    }

    // ADR-0009 D1: the 14-day window is inclusive — exactly day 14 is still open, day 14 + 1s is closed.
    [Fact]
    public void DayFourteen_IsTheInclusiveBoundary()
    {
        Assert.True(RefundPolicy.IsWithinWindow(Now.AddDays(-14), Now));
        Assert.False(RefundPolicy.IsWithinWindow(Now.AddDays(-14).AddSeconds(-1), Now));
    }

    // TC-REFUND-WINDOW — past 14 days is closed (override required).
    [Fact]
    public void PastFourteenDays_IsClosed()
    {
        Assert.False(RefundPolicy.IsWithinWindow(Now.AddDays(-15), Now));
    }

    // TC-REFUND-WINDOW — null CompletedAt is closed-by-default.
    [Fact]
    public void NullCompletedAt_IsClosedByDefault()
    {
        Assert.False(RefundPolicy.IsWithinWindow(null, Now));
    }

    // TC-REFUND-WINDOW — a Chargeback-sourced refund is window-exempt; an app-refund is window-gated.
    [Fact]
    public void Chargeback_IsExemptFromTheWindowCheck()
    {
        Assert.False(RefundPolicy.RequiresWindowCheck(RefundSource.Chargeback));
        Assert.True(RefundPolicy.RequiresWindowCheck(RefundSource.AppRefund));
    }

    // TC-REFUND-FEE — platform absorbs the Stripe fee on ServiceNotRendered / DisputeResolution.
    [Theory]
    [InlineData(RefundReason.ServiceNotRendered)]
    [InlineData(RefundReason.DisputeResolution)]
    public void PlatformAbsorbsFee_OnServiceNotRenderedAndDisputeResolution(RefundReason reason)
    {
        Assert.True(RefundPolicy.PlatformAbsorbsStripeFee(reason));
    }

    // TC-REFUND-FEE — fee deducted on AdminDiscretion; cancel-fee path untouched (not platform-absorbed).
    [Theory]
    [InlineData(RefundReason.AdminDiscretion)]
    [InlineData(RefundReason.CustomerCancellation)]
    public void FeeNotPlatformAbsorbed_OnAdminDiscretionAndCustomerCancellation(RefundReason reason)
    {
        Assert.False(RefundPolicy.PlatformAbsorbsStripeFee(reason));
    }
}
