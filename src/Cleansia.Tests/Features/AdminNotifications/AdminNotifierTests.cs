using System.Text.Json;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.AdminNotifications;

/// <summary>
/// The notifier turns one event into one feed row per administrator of the NAMED company whose role is
/// in the event's audience and one send-email outbox intent per recipient address — the company's
/// shared mailbox in English when it has set one, else every such administrator in their own language
/// (ADR-0066 D8: told what you can act on). The recipients and the mailbox are
/// read by the event's company argument, the rows carry that company, no commit is issued (the
/// caller's unit of work lands the rows with the business state), the feed rows are written before
/// the first intent so a failing outbox leaves the feed intact, and the class has no hand on a push,
/// a sending e-mail service, a queue client or the ambient settings — pinned on its constructor, so a
/// collaborator added later is a visible decision. An event whose args stray from the catalogue is a
/// programming error and is refused before a row is written.
/// </summary>
public sealed class AdminNotifierTests
{
    private const string TenantA = "company-a";
    private const string Mailbox = "ops@example.com";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserNotificationRepository> _notifications = new();
    private readonly Mock<IAppConfigurationProvider> _configuration = new();
    private readonly Mock<IPendingDispatch> _dispatch = new();
    private readonly List<UserNotification> _added = [];
    private readonly List<(string Queue, string Key, QueueEnvelope<SendAdminNotificationEmailMessage> Envelope)> _enqueued = [];

    public AdminNotifierTests()
    {
        _notifications.Setup(r => r.Add(It.IsAny<UserNotification>())).Callback<UserNotification>(_added.Add);
        _dispatch
            .Setup(d => d.Enqueue(It.IsAny<string>(), It.IsAny<QueueEnvelope<SendAdminNotificationEmailMessage>>(), It.IsAny<string>()))
            .Callback<string, QueueEnvelope<SendAdminNotificationEmailMessage>, string>((queue, envelope, key) => _enqueued.Add((queue, key, envelope)));
        ArrangeMailbox(TenantA, null);
    }

    private AdminNotifier NewNotifier(ILogger<AdminNotifier>? logger = null) =>
        new(_users.Object, _notifications.Object, _configuration.Object, _dispatch.Object, logger ?? NullLogger<AdminNotifier>.Instance);

    private void ArrangeAdministratorsSpeaking(string tenantId, params (string Id, string? Language)[] admins) =>
        ArrangeAdministratorsHolding(tenantId, admins.Select(a => (a.Id, a.Language, AdminRole.Administrator)).ToArray());

    private void ArrangeAdministratorsHolding(string tenantId, params (string Id, string? Language, AdminRole Role)[] admins) =>
        _users.Setup(r => r.GetActiveAdministratorsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admins.Select(a => new AdministratorRecipient(a.Id, $"{a.Id}@cleansia.test", "Ad", "Min", a.Language, a.Role)).ToList());

    private void ArrangeAdministrators(string tenantId, params string[] ids) =>
        ArrangeAdministratorsSpeaking(tenantId, ids.Select(id => (id, (string?)"cs")).ToArray());

    private void ArrangeMailbox(string tenantId, string? stored) =>
        _configuration
            .Setup(p => p.GetTenantSettingAsync(tenantId, TenantSettingCatalog.AdminNotificationEmailKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

    private static AdminEvent OrderNew(string tenantId, string orderId = "order-1") =>
        new(
            AdminNotificationEventCatalog.OrderNew,
            tenantId,
            Subject: orderId,
            Args: new Dictionary<string, string>
            {
                ["orderNumber"] = "ORD-1A2B3C4D",
                ["amount"] = "1500.00 CZK",
                ["paymentType"] = "Cash",
                ["countryId"] = "CZ",
                ["orderId"] = orderId,
            });

    private static AdminEvent Chargeback(string tenantId, string disputeId = "dispute-1", string orderId = "order-1") =>
        new(
            AdminNotificationEventCatalog.DisputeChargeback,
            tenantId,
            Subject: disputeId,
            Args: new Dictionary<string, string>
            {
                ["orderNumber"] = "ORD-1A2B3C4D",
                ["amount"] = "1500.00 CZK",
                ["disputeId"] = disputeId,
                ["orderId"] = orderId,
            });

    private static AdminEvent ErasureFailed(string tenantId) =>
        new(
            AdminNotificationEventCatalog.ErasureFailed,
            tenantId,
            Subject: "request-1",
            Args: new Dictionary<string, string> { ["day"] = "2026-09-19", ["requestId"] = "request-1" });

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
    public async Task An_Order_Event_Reaches_The_Support_And_Not_The_Accountant()
    {
        ArrangeAdministratorsHolding(TenantA, ("the-accountant", "cs", AdminRole.Accountant), ("the-support", "cs", AdminRole.Support));

        await NewNotifier().NotifyAsync(OrderNew(TenantA), CancellationToken.None);

        Assert.Equal("the-support", Assert.Single(_added).UserId);
        Assert.Equal("the-support@cleansia.test", Assert.Single(_enqueued).Envelope.Payload.Email);
    }

    [Fact]
    public async Task A_Chargeback_Reaches_The_Support_And_The_Accountant_Both()
    {
        ArrangeAdministratorsHolding(TenantA, ("the-accountant", "cs", AdminRole.Accountant), ("the-support", "cs", AdminRole.Support));

        await NewNotifier().NotifyAsync(Chargeback(TenantA), CancellationToken.None);

        Assert.Equal(["the-accountant", "the-support"], _added.Select(n => n.UserId));
        Assert.Equal(2, _enqueued.Count);
    }

    [Fact]
    public async Task A_Failed_Erasure_Reaches_The_Manager_And_The_Administrator_And_Nobody_Below()
    {
        ArrangeAdministratorsHolding(
            TenantA,
            ("the-administrator", "cs", AdminRole.Administrator),
            ("the-manager", "cs", AdminRole.Manager),
            ("the-support", "cs", AdminRole.Support),
            ("the-accountant", "cs", AdminRole.Accountant));

        await NewNotifier().NotifyAsync(ErasureFailed(TenantA), CancellationToken.None);

        Assert.Equal(["the-administrator", "the-manager"], _added.Select(n => n.UserId));
    }

    [Fact]
    public async Task An_Administrator_Row_Without_A_Role_Is_Told_Nothing()
    {
        _users.Setup(r => r.GetActiveAdministratorsAsync(TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AdministratorRecipient("roleless", "roleless@cleansia.test", "Ad", "Min", "cs", null)]);

        await NewNotifier().NotifyAsync(Chargeback(TenantA), CancellationToken.None);

        Assert.Empty(_added);
        Assert.Empty(_enqueued);
    }

    [Fact]
    public async Task A_Company_Whose_Administrators_Are_All_Outside_The_Audience_Writes_Nothing_Enqueues_Nothing_And_Warns()
    {
        ArrangeAdministratorsHolding(TenantA, ("the-accountant", "cs", AdminRole.Accountant));
        ArrangeMailbox(TenantA, Mailbox);
        var logger = new Mock<ILogger<AdminNotifier>>();

        await NewNotifier(logger.Object).NotifyAsync(OrderNew(TenantA), CancellationToken.None);

        Assert.Empty(_added);
        Assert.Empty(_enqueued);
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
    public async Task A_Company_With_No_Eligible_Administrator_Writes_Nothing_Enqueues_Nothing_And_Warns()
    {
        ArrangeAdministrators(TenantA);
        ArrangeMailbox(TenantA, Mailbox);
        var logger = new Mock<ILogger<AdminNotifier>>();

        await NewNotifier(logger.Object).NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);

        Assert.Empty(_added);
        Assert.Empty(_enqueued);
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
        Assert.Empty(_enqueued);
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
        Assert.Empty(_enqueued);
        _users.Verify(r => r.GetActiveAdministratorsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_Event_Without_A_Subject_Is_Refused_Before_Any_Row()
    {
        ArrangeAdministrators(TenantA, "admin-1");
        var subjectless = DisputeFiled(TenantA) with { Subject = " " };

        await Assert.ThrowsAsync<ArgumentException>(() => NewNotifier().NotifyAsync(subjectless, CancellationToken.None));

        Assert.Empty(_added);
        Assert.Empty(_enqueued);
    }

    [Fact]
    public async Task A_Key_Outside_The_Catalogue_Is_Refused()
    {
        var unknown = DisputeFiled(TenantA) with { Key = NotificationEventCatalog.OrderConfirmed };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => NewNotifier().NotifyAsync(unknown, CancellationToken.None));

        Assert.Empty(_added);
        Assert.Empty(_enqueued);
    }

    [Fact]
    public async Task With_No_Mailbox_Every_Administrator_Is_Emailed_In_Their_Own_Language_Under_A_Distinct_Key()
    {
        ArrangeAdministratorsSpeaking(TenantA, ("admin-1", "cs"), ("admin-2", "uk"), ("admin-3", null), ("admin-4", "de"));

        await NewNotifier().NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);

        Assert.Equal(4, _enqueued.Count);
        Assert.All(_enqueued, e => Assert.Equal(QueueNames.SendEmail, e.Queue));
        Assert.Equal(4, _enqueued.Select(e => e.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            [("admin-1@cleansia.test", "cs"), ("admin-2@cleansia.test", "uk"), ("admin-3@cleansia.test", "en"), ("admin-4@cleansia.test", "en")],
            _enqueued.Select(e => (e.Envelope.Payload.Email, e.Envelope.Payload.LanguageCode)));
        Assert.All(_enqueued, e =>
        {
            Assert.Equal(e.Key, e.Envelope.MessageKey);
            Assert.Equal(TenantA, e.Envelope.TenantId);
            Assert.Equal(TenantA, e.Envelope.Payload.TenantId);
            Assert.Equal(AdminNotificationEventCatalog.DisputeFiled, e.Envelope.Payload.EventKey);
            Assert.Equal("dispute-1", e.Envelope.Payload.Subject);
            Assert.Equal(SendAdminNotificationEmailMessage.Discriminator, e.Envelope.Payload.MessageType);
            Assert.Equal("ORD-1A2B3C4D", e.Envelope.Payload.Args["orderNumber"]);
            Assert.Equal(4, e.Envelope.Payload.Args.Count);
            Assert.DoesNotContain("@", e.Key);
        });
    }

    [Fact]
    public async Task With_A_Mailbox_Exactly_One_English_Email_Goes_There_And_The_Feed_Rows_Are_Still_One_Per_Administrator()
    {
        ArrangeAdministratorsSpeaking(TenantA, ("admin-1", "cs"), ("admin-2", "uk"));
        ArrangeMailbox(TenantA, Mailbox);

        await NewNotifier().NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);

        Assert.Equal(["admin-1", "admin-2"], _added.Select(n => n.UserId));
        var intent = Assert.Single(_enqueued);
        Assert.Equal(Mailbox, intent.Envelope.Payload.Email);
        Assert.Equal("en", intent.Envelope.Payload.LanguageCode);
        Assert.Equal(MessageKeys.AdminNotificationEmail(AdminNotificationEventCatalog.DisputeFiled, "dispute-1", Mailbox), intent.Key);
    }

    [Fact]
    public async Task A_Stored_Mailbox_The_Catalogue_Rejects_Falls_Back_To_Every_Administrator()
    {
        ArrangeAdministratorsSpeaking(TenantA, ("admin-1", "cs"));
        ArrangeMailbox(TenantA, "not an address");

        await NewNotifier().NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);

        Assert.Equal("admin-1@cleansia.test", Assert.Single(_enqueued).Envelope.Payload.Email);
    }

    [Fact]
    public async Task Reads_The_Mailbox_By_The_Event_Company_And_Never_The_Ambient_One()
    {
        ArrangeAdministrators(TenantA, "admin-1");
        ArrangeMailbox("company-b", "b-ops@example.com");
        _configuration
            .Setup(p => p.GetTenantSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ambient@example.com");

        await NewNotifier().NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);

        _configuration.Verify(
            p => p.GetTenantSettingAsync(TenantA, TenantSettingCatalog.AdminNotificationEmailKey, It.IsAny<CancellationToken>()),
            Times.Once);
        _configuration.Verify(p => p.GetTenantSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("admin-1@cleansia.test", Assert.Single(_enqueued).Envelope.Payload.Email);
    }

    [Fact]
    public async Task The_Outbox_Key_Is_A_Pure_Function_Of_Event_Subject_And_Address_So_A_Repeat_In_One_Request_Collapses()
    {
        ArrangeAdministratorsSpeaking(TenantA, ("admin-1", "cs"));
        var notifier = NewNotifier();

        await notifier.NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);
        await notifier.NotifyAsync(DisputeFiled(TenantA), CancellationToken.None);
        await notifier.NotifyAsync(DisputeFiled(TenantA, disputeId: "dispute-2"), CancellationToken.None);

        Assert.Equal(3, _enqueued.Count);
        Assert.Equal(_enqueued[0].Key, _enqueued[1].Key);
        Assert.NotEqual(_enqueued[0].Key, _enqueued[2].Key);
    }

    [Fact]
    public async Task The_Feed_Rows_Are_Written_Before_The_First_Email_Intent_So_A_Failing_Outbox_Leaves_The_Feed()
    {
        ArrangeAdministrators(TenantA, "admin-1", "admin-2");
        _dispatch
            .Setup(d => d.Enqueue(It.IsAny<string>(), It.IsAny<QueueEnvelope<SendAdminNotificationEmailMessage>>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("outbox unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewNotifier().NotifyAsync(DisputeFiled(TenantA), CancellationToken.None));

        Assert.Equal(["admin-1", "admin-2"], _added.Select(n => n.UserId));
    }

    [Fact]
    public void Has_No_Hand_On_A_Push_A_Sending_Email_Service_A_Queue_Client_The_Unit_Of_Work_Or_The_Ambient_Tenant()
    {
        var collaborators = typeof(AdminNotifier).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Equal(
            [typeof(IUserRepository), typeof(IUserNotificationRepository), typeof(IAppConfigurationProvider), typeof(IPendingDispatch), typeof(ILogger<AdminNotifier>)],
            collaborators);
        Assert.DoesNotContain(typeof(INotificationProducer), collaborators);
        Assert.DoesNotContain(typeof(IEmailService), collaborators);
        Assert.DoesNotContain(typeof(IQueueClient), collaborators);
        Assert.DoesNotContain(typeof(IUnitOfWork), collaborators);
        Assert.DoesNotContain(typeof(ITenantProvider), collaborators);
    }
}
