using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The administrator's feed on the Admin host (AdminNotificationController), end to end against the
/// real auth/authz pipeline: anonymous is 401 and an Employee or Customer token is 403 on all four
/// routes; an administrator sees only the <c>admin.*</c> rows of their own user — never a partner-app
/// row of the same person, never another administrator's row; marking another administrator's row
/// read is refused as not found; and a mark-read leaves NO admin audit row, because a bell click is
/// not a ledger entry.
/// </summary>
public sealed class AdminNotificationRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string GetPagedRoute = "/api/AdminNotification/get-paged";
    private const string UnreadCountRoute = "/api/AdminNotification/unread-count";
    private const string MarkReadRoute = "/api/AdminNotification/mark-read";
    private const string MarkAllReadRoute = "/api/AdminNotification/mark-all-read";
    private const string AdminOneId = "notif-admin-1";
    private const string AdminTwoId = "notif-admin-2";

    private string _ownDisputeRowId = default!;
    private string _ownOrderRowId = default!;
    private string _ownPartnerRowId = default!;
    private string _otherAdminsRowId = default!;

    private static string AdminToken(string userId) =>
        TestJwtFactory.Mint(AdminAudience, userId, $"{userId}@hosttests.local", UserProfile.Administrator, tenantId: HostTestTenants.A);

    private Task SeedFeedAsync() =>
        SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var adminOne = DomainSeed.Admin($"{AdminOneId}@hosttests.local", HostTestTenants.A);
            adminOne.Id = AdminOneId;
            var adminTwo = DomainSeed.Admin($"{AdminTwoId}@hosttests.local", HostTestTenants.A);
            adminTwo.Id = AdminTwoId;
            ctx.Users.AddRange(adminOne, adminTwo);

            var ownDispute = UserNotification.Create(AdminOneId, AdminNotificationEventCatalog.DisputeFiled,
                """{"orderNumber":"ORD-1","reason":"QualityIssue","disputeId":"dispute-1","orderId":"order-1"}""", HostTestTenants.A);
            var ownOrder = UserNotification.Create(AdminOneId, AdminNotificationEventCatalog.OrderNew,
                """{"orderNumber":"ORD-2","amount":"1 250 Kč","paymentType":"Card","countryId":"CZ","orderId":"order-2"}""", HostTestTenants.A);
            var ownPartner = UserNotification.Create(AdminOneId, NotificationEventCatalog.OrderSeatOpen,
                """{"orderNumber":"ORD-3","orderId":"order-3"}""", HostTestTenants.A);
            var otherAdmins = UserNotification.Create(AdminTwoId, AdminNotificationEventCatalog.DisputeFiled,
                """{"orderNumber":"ORD-1","reason":"QualityIssue","disputeId":"dispute-1","orderId":"order-1"}""", HostTestTenants.A);
            ctx.AddRange(ownDispute, ownOrder, ownPartner, otherAdmins);

            _ownDisputeRowId = ownDispute.Id;
            _ownOrderRowId = ownOrder.Id;
            _ownPartnerRowId = ownPartner.Id;
            _otherAdminsRowId = otherAdmins.Id;
        });

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private Task<List<UserNotification>> RowsAsync() =>
        QueryAsync(ctx => ctx.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());

    [Fact]
    public async Task Anonymous_Is_401_On_Every_Route()
    {
        var client = AdminClientAnonymous();

        HttpAssert.IsUnauthorized(await client.GetAsync(GetPagedRoute));
        HttpAssert.IsUnauthorized(await client.GetAsync(UnreadCountRoute));
        HttpAssert.IsUnauthorized(await client.PostAsJsonAsync(MarkReadRoute, new { id = "row-1" }));
        HttpAssert.IsUnauthorized(await client.PostAsJsonAsync(MarkAllReadRoute, new { }));
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Customer)]
    public async Task A_Non_Administrator_Is_403_On_Every_Route(UserProfile profile)
    {
        var client = AdminClient(TestJwtFactory.Mint(AdminAudience, "not-admin-1", "not-admin-1@hosttests.local", profile));

        HttpAssert.IsForbidden(await client.GetAsync(GetPagedRoute));
        HttpAssert.IsForbidden(await client.GetAsync(UnreadCountRoute));
        HttpAssert.IsForbidden(await client.PostAsJsonAsync(MarkReadRoute, new { id = "row-1" }));
        HttpAssert.IsForbidden(await client.PostAsJsonAsync(MarkAllReadRoute, new { }));
    }

    [Fact]
    public async Task An_Administrator_Sees_And_Counts_Only_Their_Own_Admin_Rows()
    {
        await SeedFeedAsync();
        var client = AdminClient(AdminToken(AdminOneId));

        var page = await client.GetAsync(GetPagedRoute);
        HttpAssert.IsOk(page);
        var body = await BodyAsync(page);
        Assert.Equal(2, body.GetProperty("total").GetInt32());
        var ids = body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("id").GetString()).ToHashSet();
        Assert.Equal(new HashSet<string?> { _ownDisputeRowId, _ownOrderRowId }, ids);
        Assert.DoesNotContain(_ownPartnerRowId, ids);
        Assert.DoesNotContain(_otherAdminsRowId, ids);
        var dispute = Assert.Single(body.GetProperty("data").EnumerateArray(), row => row.GetProperty("id").GetString() == _ownDisputeRowId);
        Assert.Equal(AdminNotificationEventCatalog.DisputeFiled, dispute.GetProperty("eventKey").GetString());
        Assert.Equal("dispute-1", dispute.GetProperty("args").GetProperty("disputeId").GetString());

        var count = await client.GetAsync(UnreadCountRoute);
        HttpAssert.IsOk(count);
        Assert.Equal(2, (await BodyAsync(count)).GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Marking_Another_Administrators_Row_Or_An_Own_Partner_Row_Read_Is_Not_Found_And_Changes_Nothing()
    {
        await SeedFeedAsync();
        var client = AdminClient(AdminToken(AdminOneId));

        await HttpAssert.AssertBusinessErrorAsync(
            await client.PostAsJsonAsync(MarkReadRoute, new { id = _otherAdminsRowId }), BusinessErrorMessage.NotFound);
        await HttpAssert.AssertBusinessErrorAsync(
            await client.PostAsJsonAsync(MarkReadRoute, new { id = _ownPartnerRowId }), BusinessErrorMessage.NotFound);

        Assert.All(await RowsAsync(), row => Assert.Null(row.ReadOn));
    }

    [Fact]
    public async Task A_Mark_Read_And_A_Mark_All_Read_Stamp_Only_The_Callers_Admin_Rows_And_Leave_No_Admin_Audit_Row()
    {
        await SeedFeedAsync();
        var client = AdminClient(AdminToken(AdminOneId));

        var markRead = await client.PostAsJsonAsync(MarkReadRoute, new { id = _ownDisputeRowId });
        HttpAssert.IsOk(markRead);
        Assert.Equal(_ownDisputeRowId, (await BodyAsync(markRead)).GetProperty("id").GetString());
        Assert.Equal(1, (await BodyAsync(await client.GetAsync(UnreadCountRoute))).GetProperty("count").GetInt32());

        var markAll = await client.PostAsJsonAsync(MarkAllReadRoute, new { });
        HttpAssert.IsOk(markAll);
        Assert.Equal(1, (await BodyAsync(markAll)).GetProperty("markedCount").GetInt32());
        Assert.Equal(0, (await BodyAsync(await client.GetAsync(UnreadCountRoute))).GetProperty("count").GetInt32());

        var rows = await RowsAsync();
        Assert.NotNull(Assert.Single(rows, r => r.Id == _ownDisputeRowId).ReadOn);
        Assert.NotNull(Assert.Single(rows, r => r.Id == _ownOrderRowId).ReadOn);
        Assert.Null(Assert.Single(rows, r => r.Id == _ownPartnerRowId).ReadOn);
        Assert.Null(Assert.Single(rows, r => r.Id == _otherAdminsRowId).ReadOn);

        Assert.Empty(await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().ToListAsync()));
    }
}
