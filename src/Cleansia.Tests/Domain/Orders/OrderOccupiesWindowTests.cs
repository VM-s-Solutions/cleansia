using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Domain.Orders;

/// <summary>
/// <see cref="Order.OccupiesWindow"/> is the in-memory twin of the overlap scan's time terms. The window
/// is half-open, and the scan floor is part of the answer: a row starting before
/// <c>windowStart − MaxOrderSpanHours</c> occupies nothing however long it runs, because the
/// single-window query never reads it.
/// </summary>
public class OrderOccupiesWindowTests
{
    private static readonly DateTime WindowStart = new(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = WindowStart.AddHours(1);

    [Theory]
    [InlineData(-120, 120, false)]
    [InlineData(60, 120, false)]
    [InlineData(-119, 120, true)]
    [InlineData(59, 120, true)]
    [InlineData(15, 30, true)]
    [InlineData(-60, 180, true)]
    public void The_Window_Is_Half_Open_At_Both_Ends(int startOffsetMinutes, int estimatedTimeMinutes, bool occupies)
    {
        Assert.Equal(
            occupies,
            Order.OccupiesWindow(WindowStart.AddMinutes(startOffsetMinutes), estimatedTimeMinutes, WindowStart, WindowEnd));
    }

    [Fact]
    public void A_Row_Starting_Exactly_On_The_Scan_Floor_Occupies_The_Window()
    {
        Assert.True(Order.OccupiesWindow(
            WindowStart.AddHours(-Order.MaxOrderSpanHours),
            (Order.MaxOrderSpanHours * 60) + 30,
            WindowStart,
            WindowEnd));
    }

    [Fact]
    public void A_Row_Starting_Before_The_Scan_Floor_Occupies_Nothing_However_Long_It_Runs()
    {
        Assert.False(Order.OccupiesWindow(
            WindowStart.AddHours(-Order.MaxOrderSpanHours).AddMinutes(-1),
            (Order.MaxOrderSpanHours * 60) + 90,
            WindowStart,
            WindowEnd));
    }

    /// <summary>
    /// The floor is measured from each window's own start, so one long row can occupy an earlier window
    /// and not a later one it also runs through.
    /// </summary>
    [Fact]
    public void The_Floor_Belongs_To_The_Window_Being_Asked_About()
    {
        var laterStart = WindowStart.AddHours(100);
        var rowStart = laterStart.AddHours(-Order.MaxOrderSpanHours).AddMinutes(-1);
        const int rowMinutes = 200 * 60;

        Assert.True(Order.OccupiesWindow(rowStart, rowMinutes, WindowStart, WindowEnd));
        Assert.False(Order.OccupiesWindow(rowStart, rowMinutes, laterStart, laterStart.AddHours(1)));
    }
}
