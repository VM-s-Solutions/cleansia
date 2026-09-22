using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Tests.Features.Memberships;

public class RecurringPauseLatchTests
{
    private static readonly DateTime Now = new(2030, 4, 10, 12, 0, 0, DateTimeKind.Utc);

    private static UserMembership PaidMembership()
    {
        var membership = UserMembership.Create("user", "plan", "currency", "sub", Now.AddDays(-10), Now.AddDays(20));
        membership.RecordRecurringPauseState("active", Now.AddDays(-10), Now.AddDays(-10));
        return membership;
    }

    [Fact]
    public void Repeated_sweeps_and_unpaid_updates_keep_the_latch_but_paid_same_period_recovery_rearms_it()
    {
        var membership = PaidMembership();
        membership.UpdateFromStripeWebhook("past_due", Now.AddDays(-10), Now.AddDays(20), null);
        Assert.True(membership.TryMarkRecurringPauseNotificationSent(Now));
        Assert.False(membership.TryMarkRecurringPauseNotificationSent(Now.AddDays(1)));
        membership.UpdateFromStripeWebhook("paused", Now.AddDays(-10), Now.AddDays(30), null);
        Assert.False(membership.TryMarkRecurringPauseNotificationSent(Now));
        Assert.Equal(1, membership.RecurringPauseNotificationSequence);
        membership.UpdateFromStripeWebhook("active", Now.AddDays(-10), Now.AddDays(20), null);
        membership.RecordRecurringPauseState("active", Now.AddMinutes(1), Now.AddMinutes(1));
        Assert.Null(membership.RecurringPauseNotificationSentAt);
        Assert.Equal(1, membership.RecurringPauseNotificationSequence);
        membership.UpdateFromStripeWebhook("past_due", Now.AddDays(-10), Now.AddDays(20), null);
        Assert.True(membership.TryMarkRecurringPauseNotificationSent(Now.AddMinutes(2)));
        Assert.Equal(2, membership.RecurringPauseNotificationSequence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("trialing")]
    [InlineData("past_due")]
    [InlineData("paused")]
    [InlineData("incomplete")]
    [InlineData("incomplete_expired")]
    [InlineData("canceled")]
    public void Unpaid_create_or_swap_status_cannot_establish_proof_or_rearm_a_lapse(string? status)
    {
        var unproven = UserMembership.Create("user", "plan", "currency", "sub", Now, Now.AddMonths(1));
        unproven.RecordRecurringPauseState(status, Now, Now);
        Assert.Null(unproven.PaidPeriodConfirmedAt);
        var membership = PaidMembership();
        membership.UpdateFromStripeWebhook("past_due", Now.AddDays(-10), Now.AddDays(20), null);
        Assert.True(membership.TryMarkRecurringPauseNotificationSent(Now));
        membership.ApplyPlanSwap("yearly", Now, Now.AddYears(1));
        membership.RecordRecurringPauseState(status, Now.AddMinutes(1), Now.AddMinutes(1));
        Assert.Equal(Now, membership.RecurringPauseNotificationSentAt);
        Assert.Equal(Now.AddDays(-10), membership.PaidPeriodConfirmedAt);
        Assert.Equal(MembershipStatus.PastDue, membership.Status);
    }

    [Fact]
    public void A_paid_swap_rearms_an_expired_membership_without_changing_its_entitlement_status()
    {
        var membership = UserMembership.Create("user", "plan", "currency", "sub", Now.AddDays(-30), Now.AddHours(-1));
        membership.RecordRecurringPauseState("active", Now.AddDays(-30), Now.AddDays(-30));
        Assert.True(membership.TryMarkRecurringPauseNotificationSent(Now));
        membership.ApplyPlanSwap("yearly", Now, Now.AddYears(1));
        Assert.NotNull(membership.RecurringPauseNotificationSentAt);
        membership.RecordRecurringPauseState("active", Now.AddMinutes(1), Now.AddMinutes(1));
        Assert.Null(membership.RecurringPauseNotificationSentAt);
        Assert.Equal(1, membership.RecurringPauseNotificationSequence);
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("canceled")]
    [InlineData("past_due")]
    public void Unmarked_existing_membership_does_not_invent_historical_paid_proof(string status)
    {
        var membership = UserMembership.Create("user", "plan", "currency", "sub", Now.AddDays(-30), Now.AddHours(-1));
        membership.UpdateFromStripeWebhook(status, membership.CurrentPeriodStart, membership.CurrentPeriodEnd, null);
        Assert.False(membership.TryMarkRecurringPauseNotificationSent(Now));
        Assert.Null(membership.PaidPeriodConfirmedAt);
    }

    [Fact]
    public void Trial_and_expired_periods_cannot_establish_paid_proof_even_with_active_status()
    {
        var trial = UserMembership.Create("user", "plan", "currency", "sub", Now, Now.AddMonths(1), Now.AddDays(7));
        trial.RecordRecurringPauseState("active", Now, Now);
        Assert.Null(trial.PaidPeriodConfirmedAt);
        var expired = UserMembership.Create("user", "plan", "currency", "sub", Now.AddMonths(-1), Now);
        expired.RecordRecurringPauseState("active", Now, Now);
        Assert.Null(expired.PaidPeriodConfirmedAt);
        var swappedTrial = PaidMembership();
        swappedTrial.UpdateFromStripeWebhook("past_due", Now.AddDays(-10), Now.AddDays(20), null);
        Assert.True(swappedTrial.TryMarkRecurringPauseNotificationSent(Now));
        swappedTrial.RecordRecurringPauseState("active", Now.AddMinutes(1), Now.AddMinutes(1), Now.AddDays(7));
        Assert.Equal(Now, swappedTrial.RecurringPauseNotificationSentAt);
    }

    [Fact]
    public void Genuine_paid_recovery_created_before_the_notice_but_delivered_after_it_rearms_the_next_lapse()
    {
        var membership = PaidMembership();
        membership.UpdateFromStripeWebhook("past_due", Now.AddDays(-10), Now.AddDays(20), null);
        membership.RecordRecurringPauseState("past_due", Now.AddMinutes(-10), Now.AddMinutes(-10));
        Assert.True(membership.TryMarkRecurringPauseNotificationSent(Now));

        membership.UpdateFromStripeWebhook("active", Now.AddDays(-10), Now.AddDays(20), null);
        membership.RecordRecurringPauseState("active", Now.AddMinutes(-5), Now.AddMinutes(2));

        Assert.Null(membership.RecurringPauseNotificationSentAt);
        Assert.Equal(Now.AddMinutes(-5), membership.PaidPeriodConfirmedAt);
        Assert.Equal(Now.AddMinutes(-5), membership.RecurringPauseStateObservedAt);
        Assert.Equal(1, membership.RecurringPauseNotificationSequence);
        membership.UpdateFromStripeWebhook("past_due", Now.AddDays(-10), Now.AddDays(20), null);
        membership.RecordRecurringPauseState("past_due", Now.AddMinutes(3), Now.AddMinutes(5));
        Assert.True(membership.TryMarkRecurringPauseNotificationSent(Now.AddMinutes(6)));
        Assert.Equal(2, membership.RecurringPauseNotificationSequence);
    }

    [Fact]
    public void Missing_replayed_and_pre_lapse_paid_observations_cannot_clear_a_later_lapse()
    {
        var membership = PaidMembership();
        membership.UpdateFromStripeWebhook("past_due", Now.AddDays(-10), Now.AddDays(20), null);
        membership.RecordRecurringPauseState("past_due", Now.AddMinutes(-30), Now.AddMinutes(-30));
        Assert.True(membership.TryMarkRecurringPauseNotificationSent(Now));
        foreach (var observed in new[] { default(DateTime), Now.AddDays(-11), Now.AddDays(-10), Now.AddHours(-1) })
        {
            membership.RecordRecurringPauseState("active", observed, Now.AddMinutes(1));
            Assert.Equal(Now, membership.RecurringPauseNotificationSentAt);
        }
        Assert.Equal(Now.AddDays(-10), membership.PaidPeriodConfirmedAt);
        Assert.Equal(Now.AddMinutes(-30), membership.RecurringPauseStateObservedAt);
        Assert.Equal(1, membership.RecurringPauseNotificationSequence);
        membership.RecordRecurringPauseState("active", Now.AddMinutes(1), Now.AddMinutes(1));
        Assert.Null(membership.RecurringPauseNotificationSentAt);
        membership.RecordRecurringPauseState("past_due", Now.AddMinutes(2), Now.AddMinutes(2));
        Assert.True(membership.TryMarkRecurringPauseNotificationSent(Now.AddMinutes(2)));
        membership.RecordRecurringPauseState("active", Now.AddMinutes(1), Now.AddMinutes(3));
        Assert.Equal(Now.AddMinutes(2), membership.RecurringPauseNotificationSentAt);
        Assert.Equal(2, membership.RecurringPauseNotificationSequence);
    }
}
