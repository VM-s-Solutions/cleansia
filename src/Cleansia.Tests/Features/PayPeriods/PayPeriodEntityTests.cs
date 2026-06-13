using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// Pure-entity lifecycle guards for <see cref="PayPeriod"/>: Close requires Open,
/// MarkAsPaid requires Closed, Reopen refuses a Paid period. The legal transitions are pinned too so
/// an illegal-jump regression is caught from both sides.
/// </summary>
public class PayPeriodEntityTests
{
    // ── Close ────────────────────────────────────────────────────────

    [Fact]
    public void Close_From_Open_Sets_Closed_With_Audit()
    {
        var period = PayrollMockFactory.OpenPeriod();

        period.Close("admin@cleansia.cz", "all settled");

        Assert.Equal(PayPeriodStatus.Closed, period.Status);
        Assert.NotNull(period.ClosedAt);
        Assert.Equal("admin@cleansia.cz", period.ClosedBy);
        Assert.Equal("all settled", period.Notes);
    }

    [Fact]
    public void Close_From_Closed_Throws()
    {
        var period = PayrollMockFactory.ClosedPeriod();

        Assert.Throws<InvalidOperationException>(() => period.Close("admin@cleansia.cz"));
    }

    [Fact]
    public void Close_From_Paid_Throws()
    {
        var period = PayrollMockFactory.PaidPeriod();

        Assert.Throws<InvalidOperationException>(() => period.Close("admin@cleansia.cz"));
    }

    // ── MarkAsPaid ───────────────────────────────────────────────────

    [Fact]
    public void MarkAsPaid_From_Closed_Sets_Paid()
    {
        var period = PayrollMockFactory.ClosedPeriod();

        period.MarkAsPaid();

        Assert.Equal(PayPeriodStatus.Paid, period.Status);
        Assert.NotNull(period.PaidAt);
    }

    [Fact]
    public void MarkAsPaid_From_Open_Throws()
    {
        var period = PayrollMockFactory.OpenPeriod();

        Assert.Throws<InvalidOperationException>(() => period.MarkAsPaid());
    }

    [Fact]
    public void MarkAsPaid_From_Paid_Throws()
    {
        var period = PayrollMockFactory.PaidPeriod();

        Assert.Throws<InvalidOperationException>(() => period.MarkAsPaid());
    }

    // ── Reopen ───────────────────────────────────────────────────────

    [Fact]
    public void Reopen_From_Closed_Restores_Open_And_Clears_ClosedMetadata()
    {
        var period = PayrollMockFactory.ClosedPeriod();

        period.Reopen("correction");

        Assert.Equal(PayPeriodStatus.Open, period.Status);
        Assert.Null(period.ClosedAt);
        Assert.Null(period.ClosedBy);
    }

    [Fact]
    public void Reopen_From_Paid_Throws()
    {
        var period = PayrollMockFactory.PaidPeriod();

        Assert.Throws<InvalidOperationException>(() => period.Reopen());
    }
}
