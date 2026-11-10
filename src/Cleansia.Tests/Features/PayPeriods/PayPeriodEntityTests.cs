using System.Globalization;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;

namespace Cleansia.Tests.Features.PayPeriods;

/// <summary>
/// Pure-entity lifecycle guards for <see cref="PayPeriod"/>: Close requires Open and MarkAsPaid requires
/// Closed, and nothing reopens a period. The legal transitions are pinned too so an illegal-jump regression is
/// caught from both sides.
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

    // ── Update ───────────────────────────────────────────────────────

    [Fact]
    public void Update_To_A_Six_Day_Period_Moves_The_End()
    {
        var period = PayrollMockFactory.OpenPeriod();

        period.Update(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), PayPeriod.LengthInDays);

        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6)), (period.StartDate, period.EndDate));
    }

    [Fact]
    public void Update_Past_The_Allowed_Length_Throws()
    {
        var period = PayrollMockFactory.OpenPeriod();

        Assert.Throws<ArgumentException>(() =>
            period.Update(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), PayPeriod.LengthInDays));
        Assert.Equal(new DateOnly(2026, 1, 14), period.EndDate);
    }

    [Fact]
    public void Close_With_An_End_Day_Inside_The_Period_Ends_It_That_Day()
    {
        var period = PayrollMockFactory.OpenPeriod();

        period.Close("admin@cleansia.cz", endDay: new DateOnly(2026, 1, 5));

        Assert.Equal((PayPeriodStatus.Closed, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5)),
            (period.Status, period.StartDate, period.EndDate));
    }

    [Fact]
    public void Close_On_Its_First_Day_Leaves_A_One_Day_Period()
    {
        var period = PayrollMockFactory.OpenPeriod();

        period.Close("admin@cleansia.cz", endDay: new DateOnly(2026, 1, 1));

        Assert.Equal(new DateOnly(2026, 1, 1), period.EndDate);
    }

    [Fact]
    public void Close_With_An_End_Day_Before_The_Start_Throws_And_Leaves_It_Open()
    {
        var period = PayrollMockFactory.OpenPeriod();

        Assert.Throws<ArgumentException>(() => period.Close("admin@cleansia.cz", endDay: new DateOnly(2025, 12, 31)));
        Assert.Equal((PayPeriodStatus.Open, new DateOnly(2026, 1, 14)), (period.Status, period.EndDate));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(null)]
    public void Close_With_An_End_Day_After_The_End_Or_None_Keeps_The_End(int? endDay)
    {
        var period = PayrollMockFactory.OpenPeriod();

        period.Close("admin@cleansia.cz", endDay: endDay is null ? null : new DateOnly(2026, 1, endDay.Value));

        Assert.Equal(new DateOnly(2026, 1, 14), period.EndDate);
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

    [Fact]
    public void The_Blob_Folder_Is_The_Same_Under_Any_Server_Culture()
    {
        var hostCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");

            Assert.Equal(
                "2026-08-01_2026-08-14",
                PayPeriod.BlobFolder(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 14)));
        }
        finally
        {
            CultureInfo.CurrentCulture = hostCulture;
        }
    }
}
