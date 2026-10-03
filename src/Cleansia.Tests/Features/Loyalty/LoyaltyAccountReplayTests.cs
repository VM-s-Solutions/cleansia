using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// A loyalty write that loses a race to another writer on the same account is replayed onto the row the
/// other writer committed: the commit resets the account to that row, and the points and completed bookings
/// the losing write moved go on top, so neither write's change to the totals is lost. Each account below is
/// built straight into its committed state, which is what the commit leaves before it replays.
/// </summary>
public class LoyaltyAccountReplayTests
{
    private const string UserId = "user-1";
    private const string ActorId = "system";

    private static readonly LoyaltyTierThresholds Thresholds = new(Silver: 500, Gold: 2000, Platinum: 5000);

    private static LoyaltyAccount Committed(int points, int completedBookings, LoyaltyTierThresholds thresholds)
    {
        var account = LoyaltyAccount.Create(UserId);
        for (var booking = 1; booking <= completedBookings; booking++)
        {
            account.GrantPoints(
                points / completedBookings, LoyaltyEarnSource.OrderCompleted, $"order-{booking}", ActorId, thresholds);
        }

        return account;
    }

    [Fact]
    public void A_Clawback_That_Lost_To_A_Grant_Lands_On_The_Granted_Total_And_The_Tier_Follows()
    {
        var account = Committed(points: 2200, completedBookings: 2, Thresholds);
        Assert.Equal(LoyaltyTier.GoldPolisher, account.CurrentTier);

        account.Replay(pointsMoved: -300, completedBookingsMoved: 0);

        Assert.Equal(1900, account.LifetimePoints);
        Assert.Equal(2, account.CompletedBookingsCount);
        Assert.Equal(LoyaltyTier.SilverMopper, account.CurrentTier);
    }

    [Fact]
    public void A_Completion_Grant_That_Lost_To_A_Clawback_Lands_Its_Points_And_Its_Booking()
    {
        var account = Committed(points: 600, completedBookings: 1, Thresholds);

        account.Replay(pointsMoved: 1500, completedBookingsMoved: 1);

        Assert.Equal(2100, account.LifetimePoints);
        Assert.Equal(2, account.CompletedBookingsCount);
        Assert.Equal(LoyaltyTier.GoldPolisher, account.CurrentTier);
    }

    [Fact]
    public void The_Tier_Is_Read_With_The_Thresholds_The_Write_Used()
    {
        var account = Committed(points: 1000, completedBookings: 1, Thresholds);
        var edited = new LoyaltyTierThresholds(Silver: 500, Gold: 1500, Platinum: 5000);
        account.ApplyTierThresholds(edited, ActorId);

        account.Replay(pointsMoved: 600, completedBookingsMoved: 0);

        Assert.Equal(1600, account.LifetimePoints);
        Assert.Equal(LoyaltyTier.GoldPolisher, account.CurrentTier);
    }

    [Fact]
    public void A_Replay_Never_Takes_The_Total_Below_Zero()
    {
        var account = Committed(points: 200, completedBookings: 1, Thresholds);

        account.Replay(pointsMoved: -300, completedBookingsMoved: 0);

        Assert.Equal(0, account.LifetimePoints);
        Assert.Equal(LoyaltyTier.BronzeCleaner, account.CurrentTier);
    }

    [Fact]
    public void A_Replay_That_Leaves_The_Tier_Where_It_Was_Keeps_When_It_Was_Reached()
    {
        var account = Committed(points: 2200, completedBookings: 2, Thresholds);
        var reached = account.TierAchievedOn;

        account.Replay(pointsMoved: 100, completedBookingsMoved: 0);

        Assert.Equal(2300, account.LifetimePoints);
        Assert.Equal(LoyaltyTier.GoldPolisher, account.CurrentTier);
        Assert.Equal(reached, account.TierAchievedOn);
    }
}
