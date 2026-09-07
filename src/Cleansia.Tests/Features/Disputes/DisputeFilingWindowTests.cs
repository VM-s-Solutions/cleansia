using Cleansia.Core.Domain.Disputes;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// The 24-hour reporting window (owner ruling 2026-09-05, down from the 48 the FAQ used to claim —
/// which nothing enforced, and which disagreed with the Terms' own 24).
///
/// <para>It gates the GUARANTEE, not the door: nothing here refuses a late dispute. These pin the
/// verdict an admin acts on — inside the window the platform undertook to put the job right, outside
/// it the claim is judged on its merits.</para>
/// </summary>
public class DisputeFilingWindowTests
{
    private static readonly DateTime Cleaning = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Completed = new(2026, 6, 1, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void TheWindowIsTwentyFourHours()
    {
        // The number the copy states, in five locales, and the Terms' damage line beside it.
        Assert.Equal(24, DisputeLimits.FilingWindowHours);
    }

    [Fact]
    public void ReportedTheSameEvening_IsInsideTheWindow()
    {
        Assert.True(DisputeLimits.IsWithinFilingWindow(Completed, Cleaning, Completed.AddHours(6)));
    }

    [Fact]
    public void ReportedOnTheDeadline_IsStillInside()
    {
        // Inclusive: "within 24 hours" includes the twenty-fourth.
        Assert.True(DisputeLimits.IsWithinFilingWindow(Completed, Cleaning, Completed.AddHours(24)));
    }

    [Fact]
    public void ReportedAfterTheDeadline_IsOutside()
    {
        Assert.False(DisputeLimits.IsWithinFilingWindow(
            Completed, Cleaning, Completed.AddHours(24).AddMinutes(1)));
    }

    /// <summary>
    /// The clock runs from COMPLETION when there is one — not from the scheduled start. A clean that
    /// ran late would otherwise eat hours out of the customer's window for no reason of theirs.
    /// </summary>
    [Fact]
    public void TheClockRunsFromCompletion_NotTheScheduledStart()
    {
        // 25 h after the scheduled 10:00 start, but only 22.5 h after the 12:30 finish.
        var raised = Cleaning.AddHours(25);

        Assert.True(DisputeLimits.IsWithinFilingWindow(Completed, Cleaning, raised));
        Assert.False(DisputeLimits.IsWithinFilingWindow(completedAt: null, Cleaning, raised));
    }

    /// <summary>
    /// A no-show has no completion time at all, and it is the case the window most needs to cover.
    /// The scheduled start is the fallback.
    /// </summary>
    [Fact]
    public void WithNoCompletion_TheScheduledStartIsTheReference()
    {
        Assert.True(DisputeLimits.IsWithinFilingWindow(
            completedAt: null, Cleaning, Cleaning.AddHours(20)));
        Assert.False(DisputeLimits.IsWithinFilingWindow(
            completedAt: null, Cleaning, Cleaning.AddHours(28)));
    }
}
