using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Tests for <see cref="BookingPolicy.CalculateCancellationFeeRate"/>.
///
/// Acceptance-aware policy:
///  - No acceptance yet → free, regardless of timing.
///  - Accepted, 24+ h before start → free.
///  - Accepted, 4–24 h before start → 25% (PartialCancellationFeeRate).
///  - Accepted, &lt; 4 h before start → 50% (LastMinuteCancellationFeeRate).
///  - "Oops window" overrides timing tiers when accepted.
/// </summary>
public class BookingPolicyTests
{
    private static readonly DateTime BookingCreated = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    // ── Before acceptance: always free ──

    [Fact]
    public void When_Not_Accepted_And_24h_Before_Start_Then_Fee_Is_Zero()
    {
        // Arrange
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddHours(-48); // very early

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: false);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void When_Not_Accepted_And_12h_Before_Start_Then_Fee_Is_Zero()
    {
        // Arrange
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddHours(-12);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: false);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void When_Not_Accepted_And_1h_Before_Start_Then_Fee_Is_Zero()
    {
        // Arrange
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddHours(-1);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: false);

        // Assert — "before acceptance" wins over the otherwise-50% last-minute tier.
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void When_Not_Accepted_And_FirstTimeCustomer_Then_Fee_Is_Zero()
    {
        // Arrange
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddMinutes(-30); // < 4h, would normally trigger 50%

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: true, hasBeenAccepted: false);

        // Assert
        Assert.Equal(0m, rate);
    }

    // ── After acceptance: tiered policy ──

    [Fact]
    public void When_Accepted_And_24h_Before_Start_Then_Fee_Is_Zero()
    {
        // Arrange
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddHours(-BookingPolicy.FreeCancellationHours);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: true);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void When_Accepted_And_More_Than_24h_Before_Start_Then_Fee_Is_Zero()
    {
        // Arrange
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddHours(-48);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: true);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void When_Accepted_And_12h_Before_Start_Then_Fee_Is_25_Percent()
    {
        // Arrange
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddHours(-12);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: true);

        // Assert
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, rate);
        Assert.Equal(0.25m, rate);
    }

    [Fact]
    public void When_Accepted_And_4h_Before_Start_Then_Fee_Is_25_Percent()
    {
        // Arrange — exactly 4 hours before start is the boundary; partial tier still applies.
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddHours(-BookingPolicy.PartialCancellationHours);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: true);

        // Assert
        Assert.Equal(0.25m, rate);
    }

    [Fact]
    public void When_Accepted_And_1h_Before_Start_Then_Fee_Is_50_Percent()
    {
        // Arrange
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddHours(-1);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: true);

        // Assert
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, rate);
        Assert.Equal(0.50m, rate);
    }

    [Fact]
    public void When_Accepted_And_30min_Before_Start_Then_Fee_Is_50_Percent()
    {
        // Arrange
        var cleaning = BookingCreated.AddDays(7);
        var cancel = cleaning.AddMinutes(-30);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: true);

        // Assert
        Assert.Equal(0.50m, rate);
    }

    // ── Oops window (only relevant once accepted) ──

    [Fact]
    public void When_Accepted_And_Within_Standard_Oops_Window_Then_Fee_Is_Zero()
    {
        // Arrange — cleaning is in a few hours (would normally be 50%) but cancel
        // happens within the 15-min "oops" window after booking.
        var cleaning = BookingCreated.AddHours(2);
        var cancel = BookingCreated.AddMinutes(BookingPolicy.OopsWindowMinutesStandard);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: true);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void When_Accepted_And_FirstTimeCustomer_Within_Extended_Oops_Window_Then_Fee_Is_Zero()
    {
        // Arrange — cleaning is in a few hours (would normally be 50%) but cancel
        // happens within the 60-min first-time "oops" window.
        var cleaning = BookingCreated.AddHours(2);
        var cancel = BookingCreated.AddMinutes(BookingPolicy.OopsWindowMinutesFirstTime);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: true, hasBeenAccepted: true);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void When_Accepted_And_Past_Oops_Window_And_30min_Before_Start_Then_Fee_Is_50_Percent()
    {
        // Arrange — booked 6h ago, cancel now (well past 15-min oops), 30 min before start.
        var cleaning = BookingCreated.AddHours(6);
        var cancel = cleaning.AddMinutes(-30);

        // Act
        var rate = BookingPolicy.CalculateCancellationFeeRate(
            cleaning, BookingCreated, cancel, isFirstTimeCustomer: false, hasBeenAccepted: true);

        // Assert
        Assert.Equal(0.50m, rate);
    }
}
