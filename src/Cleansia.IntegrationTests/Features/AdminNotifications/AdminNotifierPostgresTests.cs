using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
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

    private static User Administrator(string id, string tenantId, bool confirmed = true)
    {
        var user = User.CreateWithPassword($"{id}@cleansia.test", "Seed-Password-123", "Ad", "Min", UserProfile.Administrator);
        user.Id = id;
        user.TenantId = tenantId;
        if (confirmed)
        {
            user.ConfirmEmail();
        }

        return user;
    }

    private static Task SeedTwoCompanies(CleansiaDbContext context)
    {
        context.Users.AddRange(
            Administrator(AdminA1, TestTenants.Default),
            Administrator(AdminA2, TestTenants.Default),
            Administrator(AdminB1, TestTenants.Second));
        return Task.CompletedTask;
    }

    private static Task SeedIneligibleAdministrators(CleansiaDbContext context)
    {
        var deactivated = Administrator(DeactivatedA, TestTenants.Default);
        deactivated.IsActive = false;
        var anonymised = Administrator(AnonymisedA, TestTenants.Default);
        anonymised.Anonymize();
        context.Users.AddRange(
            Administrator(AdminA1, TestTenants.Default),
            deactivated,
            Administrator(UnconfirmedA, TestTenants.Default, confirmed: false),
            anonymised);
        return Task.CompletedTask;
    }

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
