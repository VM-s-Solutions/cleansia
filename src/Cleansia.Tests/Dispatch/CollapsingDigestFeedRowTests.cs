using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Moq;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// The one branch in <see cref="NotificationProducer"/> that overwrites a feed row instead of adding
/// one — which had <b>no test at all</b> until the second digest key was added to it.
///
/// <para><b>Why a set and not an equality.</b> The property that earns the collapse is the payload's
/// shape: both members answer <i>"how many, right now"</i> and neither carries a date. So a second row
/// does not merely duplicate — it <b>lies</b>. A Monday evening's <c>reminder_tomorrow</c> row still
/// reads <i>"Jobs tomorrow: 3"</i> when the cleaner opens the feed on Thursday, because nothing in the
/// payload says which Monday it was about.</para>
///
/// <para>The two per-job reminders are deliberately outside the set, and this pins that too: each is
/// about one specific job at one specific time, so a second one is new information rather than a
/// refreshed answer to the same question.</para>
/// </summary>
public class CollapsingDigestFeedRowTests
{
    private const string UserId = "user-1";

    private readonly Mock<IUserNotificationRepository> _repository = new();
    private readonly Mock<IPendingDispatch> _pendingDispatch = new();

    private NotificationProducer Producer() =>
        new(_repository.Object, _pendingDispatch.Object);

    private static Dictionary<string, string> Count(int n) => new() { ["count"] = n.ToString() };

    [Theory]
    [InlineData(NotificationEventCatalog.NewJobsAvailable)]
    [InlineData(NotificationEventCatalog.ReminderTomorrow)]
    public async Task A_Standing_Unread_Digest_Is_Refreshed_Rather_Than_Duplicated(string eventKey)
    {
        var standing = UserNotification.Create(UserId, eventKey, "{\"count\":\"3\"}", null);
        _repository
            .Setup(r => r.GetUnreadByUserAndEventAsync(UserId, eventKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(standing);

        await Producer().NotifyAsync(UserId, eventKey, Count(5), null, "subject", CancellationToken.None);

        _repository.Verify(r => r.Add(It.IsAny<UserNotification>()), Times.Never);
        Assert.Contains("5", standing.ArgsJson);
    }

    [Theory]
    [InlineData(NotificationEventCatalog.NewJobsAvailable)]
    [InlineData(NotificationEventCatalog.ReminderTomorrow)]
    public async Task With_No_Standing_Row_A_Digest_Still_Adds_One(string eventKey)
    {
        _repository
            .Setup(r => r.GetUnreadByUserAndEventAsync(UserId, eventKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotification?)null);

        await Producer().NotifyAsync(UserId, eventKey, Count(2), null, "subject", CancellationToken.None);

        _repository.Verify(r => r.Add(It.IsAny<UserNotification>()), Times.Once);
    }

    /// <summary>
    /// The negative half, and the reason this is a set rather than "collapse every feed event". This
    /// key is in the Partner keyset, so a row IS written — and it must be a second row: a cleaner taken
    /// off two different jobs has been told two different things, and collapsing them would silently
    /// destroy one.
    /// </summary>
    [Fact]
    public async Task A_Per_Job_Feed_Event_Is_Never_Collapsed()
    {
        var eventKey = NotificationEventCatalog.OrderAssignmentCancelled;
        _repository
            .Setup(r => r.GetUnreadByUserAndEventAsync(UserId, eventKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserNotification.Create(UserId, eventKey, "{}", null));

        await Producer().NotifyAsync(
            UserId, eventKey, new Dictionary<string, string>(), null, "order-1", CancellationToken.None);

        _repository.Verify(r => r.Add(It.IsAny<UserNotification>()), Times.Once);
    }

    /// <summary>
    /// Membership is the contract, so it is asserted directly — a future key added to the catalogue
    /// that answers "how many, right now" belongs here, and one that does not must stay out.
    /// </summary>
    [Fact]
    public void The_Collapsing_Set_Is_Exactly_The_Two_Count_Digests()
    {
        Assert.Equal(
            new[] { NotificationEventCatalog.NewJobsAvailable, NotificationEventCatalog.ReminderTomorrow }
                .OrderBy(k => k, StringComparer.Ordinal),
            NotificationEventCatalog.CollapsingDigestKeys.OrderBy(k => k, StringComparer.Ordinal));

        Assert.DoesNotContain(NotificationEventCatalog.ReminderSoon, NotificationEventCatalog.CollapsingDigestKeys);
        Assert.DoesNotContain(
            NotificationEventCatalog.ReminderNotStarted, NotificationEventCatalog.CollapsingDigestKeys);
    }
}
