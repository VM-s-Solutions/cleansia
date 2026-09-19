using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.AdminNotifications;

/// <summary>
/// The notifier turns one event into one feed row per administrator of the NAMED company and nothing
/// else: the recipients are read by the event's company argument, the rows carry that company, no
/// commit is issued (the caller's unit of work lands the rows with the business state), and the class
/// has no hand on a push, an e-mail, a queue or the ambient settings — pinned on its constructor, so a
/// collaborator added later is a visible decision. An event whose args stray from the catalogue is a
/// programming error and is refused before a row is written.
/// </summary>
public sealed class AdminNotifierTests
{
    private const string TenantA = "company-a";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserNotificationRepository> _notifications = new();
    private readonly List<UserNotification> _added = [];

    public AdminNotifierTests()
    {
        _notifications.Setup(r => r.Add(It.IsAny<UserNotification>())).Callback<UserNotification>(_added.Add);
    }

    private AdminNotifier NewNotifier(ILogger<AdminNotifier>? logger = null) =>
        new(_users.Object, _notifications.Object, logger ?? NullLogger<AdminNotifier>.Instance);

    private void ArrangeAdministrators(string tenantId, params string[] ids) =>
        _users.Setup(r => r.GetActiveAdministratorsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids.Select(id => new AdministratorRecipient(id, $"{id}@cleansia.test", "Ad", "Min", "cs")).ToList());

    private static AdminEvent DisputeFiled(string tenantId, string disputeId = "dispute-1", string orderId = "order-1") =>
        new(
            AdminNotificationEventCatalog.DisputeFiled,
            tenantId,
            Subject: disputeId,
            Args: new Dictionary<string, string>
            {
                ["orderNumber"] = "ORD-1A2B3C4D",
                ["reason"] = "QualityIssue",
                ["disputeId"] = disputeId,
                ["orderId"] = orderId,
            });

    [Fact]
    public async Task Writes_One_Row_Per_Administrator_Of_The_Named_Company_With_The_Args()
    {
        ArrangeAdministrators(TenantA, "admin-1", "admin-2");

        await NewNotifier().NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);

        Assert.Equal(2, _added.Count);
        Assert.Equal(["admin-1", "admin-2"], _added.Select(n => n.UserId));
        Assert.All(_added, row =>
        {
            Assert.Equal(AdminNotificationEventCatalog.DisputeFiled, row.EventKey);
            Assert.Equal(TenantA, row.TenantId);
            Assert.Null(row.ReadOn);
            var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
            Assert.Equal("ORD-1A2B3C4D", args["orderNumber"]);
            Assert.Equal("QualityIssue", args["reason"]);
            Assert.Equal("dispute-1", args["disputeId"]);
            Assert.Equal("order-1", args["orderId"]);
            Assert.Equal(4, args.Count);
        });
    }

    [Fact]
    public async Task The_Row_Args_Are_A_Flat_Object_The_Repository_Containment_Fragment_Matches()
    {
        ArrangeAdministrators(TenantA, "admin-1");

        await NewNotifier().NotifyAsync(DisputeFiled(TenantA, orderId: "order-77"), CancellationToken.None);

        using var written = JsonDocument.Parse(Assert.Single(_added).ArgsJson);
        using var fragment = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, string> { ["orderId"] = "order-77" }));
        var pair = fragment.RootElement.EnumerateObject().Single();
        Assert.Equal(JsonValueKind.Object, written.RootElement.ValueKind);
        Assert.Equal(pair.Value.GetString(), written.RootElement.GetProperty(pair.Name).GetString());
    }

    [Fact]
    public async Task Reads_The_Recipients_By_The_Event_Company_And_Nothing_Else()
    {
        ArrangeAdministrators(TenantA, "admin-1");
        ArrangeAdministrators("company-b", "admin-b");

        await NewNotifier().NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);

        _users.Verify(r => r.GetActiveAdministratorsAsync(TenantA, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.GetActiveAdministratorsAsync("company-b", It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("admin-1", Assert.Single(_added).UserId);
    }

    [Fact]
    public async Task Never_Commits()
    {
        ArrangeAdministrators(TenantA, "admin-1");

        await NewNotifier().NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);

        _notifications.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Company_With_No_Eligible_Administrator_Writes_Nothing_And_Warns()
    {
        ArrangeAdministrators(TenantA);
        var logger = new Mock<ILogger<AdminNotifier>>();

        await NewNotifier(logger.Object).NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);

        Assert.Empty(_added);
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task An_Arg_The_Catalogue_Does_Not_Declare_Is_Refused_Before_Any_Row()
    {
        ArrangeAdministrators(TenantA, "admin-1");
        var stray = DisputeFiled(TenantA) with
        {
            Args = new Dictionary<string, string> { ["orderId"] = "order-1", ["customerName"] = "Jana Nováková" },
        };

        await Assert.ThrowsAsync<ArgumentException>(() => NewNotifier().NotifyAsync(stray, CancellationToken.None));

        Assert.Empty(_added);
        _users.Verify(r => r.GetActiveAdministratorsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_Arg_The_Catalogue_Declares_But_The_Site_Omits_Is_Refused_Before_Any_Row()
    {
        ArrangeAdministrators(TenantA, "admin-1");
        var withoutTheDeepLinkId = DisputeFiled(TenantA) with
        {
            Args = new Dictionary<string, string>
            {
                ["orderNumber"] = "ORD-1A2B3C4D",
                ["reason"] = "QualityIssue",
                ["disputeId"] = "dispute-1",
            },
        };

        var refused = await Assert.ThrowsAsync<ArgumentException>(
            () => NewNotifier().NotifyAsync(withoutTheDeepLinkId, CancellationToken.None));

        Assert.Contains("orderId", refused.Message);
        Assert.Empty(_added);
        _users.Verify(r => r.GetActiveAdministratorsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Key_Outside_The_Catalogue_Is_Refused()
    {
        var unknown = DisputeFiled(TenantA) with { Key = NotificationEventCatalog.OrderConfirmed };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => NewNotifier().NotifyAsync(unknown, CancellationToken.None));

        Assert.Empty(_added);
    }

    [Fact]
    public void Has_No_Hand_On_A_Push_An_Email_A_Queue_The_Unit_Of_Work_Or_The_Ambient_Tenant()
    {
        var collaborators = typeof(AdminNotifier).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Equal([typeof(IUserRepository), typeof(IUserNotificationRepository), typeof(ILogger<AdminNotifier>)], collaborators);
        Assert.DoesNotContain(typeof(INotificationProducer), collaborators);
        Assert.DoesNotContain(typeof(IEmailService), collaborators);
        Assert.DoesNotContain(typeof(IQueueClient), collaborators);
        Assert.DoesNotContain(typeof(IPendingDispatch), collaborators);
        Assert.DoesNotContain(typeof(IAppConfigurationProvider), collaborators);
        Assert.DoesNotContain(typeof(IUnitOfWork), collaborators);
        Assert.DoesNotContain(typeof(ITenantProvider), collaborators);
    }
}
