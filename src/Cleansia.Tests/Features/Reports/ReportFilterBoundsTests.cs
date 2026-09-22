using Cleansia.Core.AppServices.Features.Reports.Filters;

namespace Cleansia.Tests.Features.Reports;

/// <summary>
/// The report bounds reach Npgsql as UTC instants whatever kind the query string bound them with — a
/// bare <c>startDate=2026-01-01</c> arrives Unspecified, and a <c>timestamp with time zone</c>
/// parameter refuses that kind outright, which the admin console met as a 500 on the revenue report.
/// </summary>
public class ReportFilterBoundsTests
{
    [Fact]
    public void An_Unspecified_Kind_Is_Read_As_Utc_Without_Moving_The_Instant()
    {
        var filter = new ReportFilter(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31, 23, 59, 59));

        var bounds = filter.AsUtc();

        Assert.Equal(DateTimeKind.Utc, bounds.StartDate.Kind);
        Assert.Equal(DateTimeKind.Utc, bounds.EndDate.Kind);
        Assert.Equal(filter.StartDate.Ticks, bounds.StartDate.Ticks);
        Assert.Equal(filter.EndDate.Ticks, bounds.EndDate.Ticks);
        Assert.Equal(filter.CurrencyId, bounds.CurrencyId);
    }

    [Fact]
    public void A_Utc_Kind_Passes_Through_Unchanged()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var bounds = new ReportFilter(start, end, "cur-czk").AsUtc();

        Assert.Equal(start, bounds.StartDate);
        Assert.Equal(end, bounds.EndDate);
        Assert.Equal("cur-czk", bounds.CurrencyId);
    }

    [Fact]
    public void A_Local_Kind_Is_Converted_To_The_Same_Instant_In_Utc()
    {
        var local = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Local);

        var bounds = new ReportFilter(local, local).AsUtc();

        Assert.Equal(DateTimeKind.Utc, bounds.StartDate.Kind);
        Assert.Equal(local.ToUniversalTime(), bounds.StartDate);
        Assert.Equal(local.ToUniversalTime(), bounds.EndDate);
    }
}
