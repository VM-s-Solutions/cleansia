using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities;

namespace Cleansia.Tests.Features.Notifications;

/// <summary>
/// A bell click is not a ledger entry. The two mark-read commands are opted out of the audit pipeline,
/// so an administrator reading their own feed on the admin host — or, as before, marking read in the
/// partner app — leaves no admin audit row; the gate answers nobody for either command, on any host.
/// </summary>
public sealed class MarkReadAuditOptOutTests
{
    public static TheoryData<object> MarkReadCommands => new()
    {
        new MarkNotificationRead.Command("row-1", NotificationFeedAudience.Admin),
        new MarkAllNotificationsRead.Command(null, NotificationFeedAudience.Admin),
    };

    [Theory]
    [MemberData(nameof(MarkReadCommands))]
    public void The_Descriptor_Is_Opted_Out(object command)
    {
        Assert.False(AuditActionDescriptor.For(command.GetType()).Audited);
    }

    [Theory]
    [MemberData(nameof(MarkReadCommands))]
    public void An_Administrator_Marking_Read_Is_Audited_On_No_Host(object command)
    {
        var descriptor = AuditActionDescriptor.For(command.GetType());
        var administrator = new TestUserSessionProvider(
            "admin-1", "admin@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);

        foreach (var host in new[] { JwtAudiences.Admin, JwtAudiences.Partner, JwtAudiences.Mobile })
        {
            Assert.Null(AuditGate.Resolve(command, descriptor, administrator, new HostAudienceProvider(host)));
        }
    }
}
