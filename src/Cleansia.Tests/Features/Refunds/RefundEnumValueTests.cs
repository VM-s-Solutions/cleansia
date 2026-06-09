using Cleansia.Core.Domain.Enums;

namespace Cleansia.Tests.Features.Refunds;

public sealed class RefundEnumValueTests
{
    [Fact]
    public void RefundReason_HasExactlyTheFourPolicyValues()
    {
        var values = Enum.GetValues<RefundReason>();

        Assert.Equal(4, values.Length);
        Assert.Contains(RefundReason.CustomerCancellation, values);
        Assert.Contains(RefundReason.DisputeResolution, values);
        Assert.Contains(RefundReason.AdminDiscretion, values);
        Assert.Contains(RefundReason.ServiceNotRendered, values);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, 1)]
    [InlineData(PaymentStatus.Paid, 2)]
    [InlineData(PaymentStatus.Failed, 3)]
    [InlineData(PaymentStatus.Refunded, 4)]
    [InlineData(PaymentStatus.Disputed, 5)]
    public void PaymentStatus_ExistingWireValues_AreUnchanged(PaymentStatus status, int wireValue)
    {
        Assert.Equal(wireValue, (int)status);
    }

    [Fact]
    public void PaymentStatus_PartiallyRefunded_IsAppendedAsSix()
    {
        Assert.Equal(6, (int)PaymentStatus.PartiallyRefunded);
    }
}
