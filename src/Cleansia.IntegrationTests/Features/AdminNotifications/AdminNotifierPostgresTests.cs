using System.Text.Json;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.AdminNotifications;

/// <summary>
/// The notifier on real Postgres: the recipients are the NAMED company's administrators even when the
/// ambient tenant is overridden to another company at the call, every row carries the named company,
/// a deactivated, unconfirmed or anonymised administrator receives nothing, and the once-per-resource
/// guard finds the row by the arg the notifier wrote — for that company, that key and that value only.
/// The e-mail leg lands beside the rows: one send-email outbox row per administrator in their own
/// language with no mailbox set, exactly one English row to the mailbox when the NAMED company set one
/// — and the other company's mailbox, the one the ambient override points at, is never the address.
/// </summary>
[Collection("PostgresCollection")]
public class AdminNotifierPostgresTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminA1 = "admin-a1";
    private const string AdminA2 = "admin-a2";
    private const string AdminB1 = "admin-b1";
    private const string DeactivatedA = "admin-a-deactivated";
    private const string UnconfirmedA = "admin-a-unconfirmed";
    private const string AnonymisedA = "admin-a-anonymised";
    private const string OrderId = "order-admin-notifier-1";

    private const string MailboxA = "ops-a@example.com";
    private const string MailboxB = "ops-b@example.com";

    private const string AccountantA = "admin-a-accountant";
    private const string SupportA = "admin-a-support";

    private static User Administrator(string id, string tenantId, bool confirmed = true, string? language = null, AdminRole role = AdminRole.Administrator)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator, language, adminRole: role);
        user.Id = id;
        user.TenantId = tenantId;
        if (confirmed)
        {
            user.ConfirmEmail();
        }

        return user;
    }

    private static TenantConfiguration Mailbox(string tenantId, string address)
    {
        var row = TenantConfiguration.Create(TenantSettingCatalog.AdminNotificationEmailKey, address, category: TenantSettingCatalog.NotificationsCategory);
        row.TenantId = tenantId;
        return row;
    }

    private static void AddTwoCompanies(CleansiaDbContext context)
    {
        context.Languages.AddRange(Language.Create("en", "English"), Language.Create("cs", "Czech"));
        context.Users.AddRange(
            Administrator(AdminA1, TestTenants.Default, language: "cs"),
            Administrator(AdminA2, TestTenants.Default),
            Administrator(AdminB1, TestTenants.Second));
    }

    private static Task Commit(CleansiaDbContext context)
    {
        StampUnstampedAdded(context, TestTenants.Default);
        return context.CommitAsync(CancellationToken.None);
    }

    private static Task SeedTwoCompanies(CleansiaDbContext context)
    {
        AddTwoCompanies(context);
        return Commit(context);
    }

    private static Task SeedTwoCompaniesWithAMailboxOnB(CleansiaDbContext context)
    {
        AddTwoCompanies(context);
        context.TenantConfigurations.Add(Mailbox(TestTenants.Second, MailboxB));
        return Commit(context);
    }

    private static Task SeedTwoCompaniesWithAMailboxOnA(CleansiaDbContext context)
    {
        AddTwoCompanies(context);
        context.TenantConfigurations.Add(Mailbox(TestTenants.Default, MailboxA));
        return Commit(context);
    }

    private static Task SeedIneligibleAdministrators(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var deactivated = Administrator(DeactivatedA, TestTenants.Default);
        deactivated.IsActive = false;
        var anonymised = Administrator(AnonymisedA, TestTenants.Default);
        anonymised.Anonymize();
        context.Users.AddRange(
            Administrator(AdminA1, TestTenants.Default),
            deactivated,
            Administrator(UnconfirmedA, TestTenants.Default, confirmed: false),
            anonymised);
        return Commit(context);
    }

    private static Task SeedAnAccountantAndASupport(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        context.Users.AddRange(
            Administrator(AccountantA, TestTenants.Default, role: AdminRole.Accountant),
            Administrator(SupportA, TestTenants.Default, role: AdminRole.Support));
        return Commit(context);
    }

    private static AdminEvent OrderNewFor(string tenantId, string orderId = OrderId) =>
        new(
            AdminNotificationEventCatalog.OrderNew,
            tenantId,
            Subject: orderId,
            Args: new Dictionary<string, string>
            {
                ["orderNumber"] = "ORD-ADMIN1",
                ["amount"] = "1500.00 CZK",
                ["paymentType"] = nameof(PaymentType.Cash),
                ["countryId"] = "CZ",
                ["orderId"] = orderId,
            });

    private static AdminEvent ChargebackFor(string tenantId, string orderId = OrderId) =>
        new(
            AdminNotificationEventCatalog.DisputeChargeback,
            tenantId,
            Subject: "dispute-1",
            Args: new Dictionary<string, string>
            {
                ["orderNumber"] = "ORD-ADMIN1",
                ["amount"] = "1500.00 CZK",
                ["disputeId"] = "dispute-1",
                ["orderId"] = orderId,
            });

    private static AdminEvent DisputeFiledFor(string tenantId, string orderId = OrderId) =>
        new(
            AdminNotificationEventCatalog.DisputeFiled,
            tenantId,
            Subject: "dispute-1",
            Args: new Dictionary<string, string>
            {
                ["orderNumber"] = "ORD-ADMIN1",
                ["reason"] = nameof(DisputeReason.QualityIssue),
                ["disputeId"] = "dispute-1",
                ["orderId"] = orderId,
            });

    private static async Task<int> RaiseUnderOverride(IServiceProvider provider, string ambientTenantId, AdminEvent adminEvent)
    {
        provider.GetRequiredService<ITenantProvider>().SetTenantOverride(ambientTenantId);
        await provider.GetRequiredService<IAdminNotifier>().NotifyAsync(adminEvent, CancellationToken.None);
        await provider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
        return 0;
    }

    private sealed record GuardAnswers(bool SameOrder, bool OtherOrder, bool OtherCompany, bool OtherKey, bool ValueUnderAnotherName);

    private static Task<List<UserNotification>> AdminRows(CleansiaDbContext context) =>
        context.Set<UserNotification>().IgnoreQueryFilters()
            .Where(n => n.EventKey == AdminNotificationEventCatalog.DisputeFiled)
            .OrderBy(n => n.UserId)
            .ToListAsync();

    private static Task<List<OutboxMessage>> EmailRows(CleansiaDbContext context) =>
        context.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.SendEmail)
            .OrderBy(m => m.MessageKey)
            .ToListAsync();

    private static QueueEnvelope<SendAdminNotificationEmailMessage> Read(OutboxMessage row) =>
        JsonSerializer.Deserialize<QueueEnvelope<SendAdminNotificationEmailMessage>>(
            row.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;

    [Fact]
    public async Task Tells_The_Named_Companys_Administrators_Even_When_The_Ambient_Tenant_Is_Another_Company()
    {
        await TestMethod(
            arrange: SeedTwoCompanies,
            act: provider => RaiseUnderOverride(provider, TestTenants.Second, DisputeFiledFor(TestTenants.Default)),
            assert: async (CleansiaDbContext context, int _) =>
            {
                var rows = await AdminRows(context);
                Assert.Equal([AdminA1, AdminA2], rows.Select(r => r.UserId));
                Assert.All(rows, row =>
                {
                    Assert.Equal(TestTenants.Default, row.TenantId);
                    Assert.Null(row.ReadOn);
                    var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
                    Assert.Equal(OrderId, args["orderId"]);
                    Assert.Equal("dispute-1", args["disputeId"]);
                });
                Assert.DoesNotContain(rows, r => r.UserId == AdminB1);
            },
            transactional: false);
    }

    // ADR-0066 D8, on the real projection: the role comes off the row, an order event is Support's alone
    // and a chargeback is both branches of the lattice.
    [Fact]
    public async Task A_New_Order_Reaches_The_Support_And_Not_The_Accountant()
    {
        await TestMethod(
            arrange: SeedAnAccountantAndASupport,
            act: provider => RaiseUnderOverride(provider, TestTenants.Default, OrderNewFor(TestTenants.Default)),
            assert: async (CleansiaDbContext context, int _) =>
            {
                var rows = await context.Set<UserNotification>().IgnoreQueryFilters()
                    .Where(n => n.EventKey == AdminNotificationEventCatalog.OrderNew)
                    .ToListAsync();
                Assert.Equal(SupportA, Assert.Single(rows).UserId);
                var email = Assert.Single(await EmailRows(context));
                Assert.Equal($"{SupportA}@cleansia.test", Read(email).Payload.Email);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Chargeback_Reaches_The_Support_And_The_Accountant_Both()
    {
        await TestMethod(
            arrange: SeedAnAccountantAndASupport,
            act: provider => RaiseUnderOverride(provider, TestTenants.Default, ChargebackFor(TestTenants.Default)),
            assert: async (CleansiaDbContext context, int _) =>
            {
                var rows = await context.Set<UserNotification>().IgnoreQueryFilters()
                    .Where(n => n.EventKey == AdminNotificationEventCatalog.DisputeChargeback)
                    .OrderBy(n => n.UserId)
                    .ToListAsync();
                Assert.Equal([AccountantA, SupportA], rows.Select(r => r.UserId));
                Assert.Equal(2, (await EmailRows(context)).Count);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Deactivated_An_Unconfirmed_And_An_Anonymised_Administrator_Receive_Nothing()
    {
        await TestMethod(
            arrange: SeedIneligibleAdministrators,
            act: provider => RaiseUnderOverride(provider, TestTenants.Default, DisputeFiledFor(TestTenants.Default)),
            assert: async (CleansiaDbContext context, int _) =>
            {
                var row = Assert.Single(await AdminRows(context));
                Assert.Equal(AdminA1, row.UserId);
                var email = Assert.Single(await EmailRows(context));
                Assert.Equal($"{AdminA1}@cleansia.test", Read(email).Payload.Email);
            },
            transactional: false);
    }

    [Fact]
    public async Task With_No_Mailbox_Every_Administrator_Of_The_Named_Company_Gets_An_Outbox_Email_In_Their_Language_And_Never_The_Other_Companys_Mailbox()
    {
        await TestMethod(
            arrange: SeedTwoCompaniesWithAMailboxOnB,
            act: provider => RaiseUnderOverride(provider, TestTenants.Second, DisputeFiledFor(TestTenants.Default)),
            assert: async (CleansiaDbContext context, int _) =>
            {
                Assert.Equal([AdminA1, AdminA2], (await AdminRows(context)).Select(r => r.UserId));

                var emails = await EmailRows(context);
                Assert.Equal(2, emails.Count);
                Assert.Equal(2, emails.Select(e => e.MessageKey).Distinct(StringComparer.Ordinal).Count());
                Assert.All(emails, row =>
                {
                    Assert.Equal(TestTenants.Default, row.TenantId);
                    var envelope = Read(row);
                    Assert.Equal(row.MessageKey, envelope.MessageKey);
                    Assert.Equal(TestTenants.Default, envelope.TenantId);
                    Assert.Equal(SendAdminNotificationEmailMessage.Discriminator, envelope.Payload.MessageType);
                    Assert.Equal(AdminNotificationEventCatalog.DisputeFiled, envelope.Payload.EventKey);
                    Assert.Equal("dispute-1", envelope.Payload.Subject);
                    Assert.Equal(OrderId, envelope.Payload.Args["orderId"]);
                    Assert.NotEqual(MailboxB, envelope.Payload.Email);
                });
                Assert.Equal(
                    [($"{AdminA1}@cleansia.test", "cs"), ($"{AdminA2}@cleansia.test", "en")],
                    emails.Select(Read).Select(e => (e.Payload.Email, e.Payload.LanguageCode)).OrderBy(e => e.Email));
            },
            transactional: false);
    }

    [Fact]
    public async Task With_A_Mailbox_On_The_Named_Company_Exactly_One_English_Email_Goes_There_And_The_Feed_Rows_Stay_Per_Administrator()
    {
        await TestMethod(
            arrange: SeedTwoCompaniesWithAMailboxOnA,
            act: provider => RaiseUnderOverride(provider, TestTenants.Second, DisputeFiledFor(TestTenants.Default)),
            assert: async (CleansiaDbContext context, int _) =>
            {
                Assert.Equal([AdminA1, AdminA2], (await AdminRows(context)).Select(r => r.UserId));

                var email = Assert.Single(await EmailRows(context));
                Assert.Equal(TestTenants.Default, email.TenantId);
                var envelope = Read(email);
                Assert.Equal(MailboxA, envelope.Payload.Email);
                Assert.Equal("en", envelope.Payload.LanguageCode);
                Assert.Equal(MessageKeys.AdminNotificationEmail(AdminNotificationEventCatalog.DisputeFiled, "dispute-1", MailboxA), email.MessageKey);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Once_Per_Resource_Guard_Finds_The_Row_By_Company_Key_And_Arg_Only()
    {
        await TestMethod(
            arrange: SeedTwoCompanies,
            act: async provider =>
            {
                await RaiseUnderOverride(provider, TestTenants.Second, DisputeFiledFor(TestTenants.Default));
                var repository = provider.GetRequiredService<IUserNotificationRepository>();
                return new GuardAnswers(
                    SameOrder: await repository.AnyForEventAsync(TestTenants.Default, AdminNotificationEventCatalog.DisputeFiled, "orderId", OrderId, CancellationToken.None),
                    OtherOrder: await repository.AnyForEventAsync(TestTenants.Default, AdminNotificationEventCatalog.DisputeFiled, "orderId", "order-other", CancellationToken.None),
                    OtherCompany: await repository.AnyForEventAsync(TestTenants.Second, AdminNotificationEventCatalog.DisputeFiled, "orderId", OrderId, CancellationToken.None),
                    OtherKey: await repository.AnyForEventAsync(TestTenants.Default, AdminNotificationEventCatalog.PaymentFailed, "orderId", OrderId, CancellationToken.None),
                    ValueUnderAnotherName: await repository.AnyForEventAsync(TestTenants.Default, AdminNotificationEventCatalog.DisputeFiled, "disputeId", OrderId, CancellationToken.None));
            },
            assert: (CleansiaDbContext _, GuardAnswers found) =>
            {
                Assert.Equal(new GuardAnswers(SameOrder: true, OtherOrder: false, OtherCompany: false, OtherKey: false, ValueUnderAnotherName: false), found);
                return Task.CompletedTask;
            },
            transactional: false);
    }
}
