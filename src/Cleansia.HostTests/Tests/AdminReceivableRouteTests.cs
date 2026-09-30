using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// What customers owe the company, on the Admin host (owner ruling 2026-09-28, decisions 17 and 18): any
/// administrator reads their own company's receivables and no other company's; only a Manager or above
/// writes one off, and the write-off records who and why; a receivable already written off, or one of
/// another company's, is refused and left as it is.
/// </summary>
public sealed class AdminReceivableRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string GetPagedRoute = "/api/AdminReceivable/get-paged";
    private const string WriteOffRoute = "/api/AdminReceivable/write-off";
    private const string ManagerId = "receivable-manager";
    private const string SupportId = "receivable-support";

    private sealed record Seeded(string OwnId, string OtherCompanysId, string WrittenOffId);

    private async Task<Seeded> ArrangeAsync()
    {
        Seeded seeded = null!;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var manager = DomainSeed.Admin($"{ManagerId}@hosttests.local", role: AdminRole.Manager);
            manager.Id = ManagerId;
            var support = DomainSeed.Admin($"{SupportId}@hosttests.local", role: AdminRole.Support);
            support.Id = SupportId;
            var customer = DomainSeed.Customer("owing@hosttests.local");
            ctx.Users.AddRange(manager, support, customer);

            var own = OwedOn(DomainSeed.NewOrder(customer.Id, customer.Email), ctx);
            var writtenOff = OwedOn(DomainSeed.NewOrder(customer.Id, customer.Email), ctx);
            writtenOff.WriteOff("earlier-admin", "Goodwill", DateTimeOffset.UtcNow.AddDays(-1));
            var otherCompanys = OwedOn(DomainSeed.NewOrder(customer.Id, customer.Email, HostTestTenants.B), ctx);
            otherCompanys.TenantId = HostTestTenants.B;

            seeded = new Seeded(own.Id, otherCompanys.Id, writtenOff.Id);
        });
        return seeded;
    }

    private static Receivable OwedOn(Core.Domain.Orders.Order order, Infra.Database.CleansiaDbContext ctx)
    {
        var receivable = Receivable.ForCashCancellationFee(order, 375m);
        ctx.Orders.Add(order);
        ctx.Receivables.Add(receivable);
        return receivable;
    }

    private HttpClient Admin(string userId, AdminRole role) =>
        AdminClient(TestJwtFactory.Mint(
            AdminAudience, userId, $"{userId}@hosttests.local", UserProfile.Administrator, adminRole: role));

    private Task<Receivable> ReadAsync(string id) =>
        QueryAsync(ctx => ctx.Receivables.IgnoreQueryFilters().SingleAsync(r => r.Id == id));

    [Fact]
    public async Task Anonymous_Is_401_And_A_Customer_Is_403_On_Both_Routes()
    {
        var customer = AdminClient(TestJwtFactory.Mint(AdminAudience, "cust-1", "cust-1@hosttests.local", UserProfile.Customer));

        HttpAssert.IsUnauthorized(await AdminClientAnonymous().GetAsync(GetPagedRoute));
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().PostAsJsonAsync(WriteOffRoute, new { receivableId = "r-1", note = "x" }));
        HttpAssert.IsForbidden(await customer.GetAsync(GetPagedRoute));
        HttpAssert.IsForbidden(await customer.PostAsJsonAsync(WriteOffRoute, new { receivableId = "r-1", note = "x" }));
    }

    [Fact]
    public async Task An_Administrator_Lists_Only_Their_Own_Companys_Receivables()
    {
        var s = await ArrangeAsync();

        var response = await Admin(SupportId, AdminRole.Support).GetAsync(GetPagedRoute);

        HttpAssert.IsOk(response);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, body.GetProperty("total").GetInt32());
        var ids = body.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToHashSet();
        Assert.Equal(new HashSet<string?> { s.OwnId, s.WrittenOffId }, ids);
        var own = Assert.Single(body.GetProperty("data").EnumerateArray(), r => r.GetProperty("id").GetString() == s.OwnId);
        Assert.Equal(375m, own.GetProperty("amount").GetDecimal());
        Assert.Equal("CZK", own.GetProperty("currencyCode").GetString());
        Assert.Equal(nameof(ReceivableKind.CashCancellationFee), own.GetProperty("kind").GetProperty("name").GetString());
        Assert.Equal(nameof(ReceivableStatus.Open), own.GetProperty("status").GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_List_Filters_By_Status()
    {
        var s = await ArrangeAsync();

        var response = await Admin(SupportId, AdminRole.Support)
            .GetAsync($"{GetPagedRoute}?Filter.Status={(int)ReceivableStatus.Open}");

        HttpAssert.IsOk(response);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(s.OwnId, Assert.Single(body.GetProperty("data").EnumerateArray()).GetProperty("id").GetString());
    }

    [Fact]
    public async Task Support_Is_Refused_The_Write_Off_And_The_Receivable_Stays_Open()
    {
        var s = await ArrangeAsync();

        var response = await Admin(SupportId, AdminRole.Support)
            .PostAsJsonAsync(WriteOffRoute, new { receivableId = s.OwnId, note = "Goodwill" });

        HttpAssert.IsForbidden(response);
        Assert.Equal(ReceivableStatus.Open, (await ReadAsync(s.OwnId)).Status);
    }

    [Fact]
    public async Task A_Manager_Writes_Off_An_Open_Receivable_With_Who_And_Why()
    {
        var s = await ArrangeAsync();

        var response = await Admin(ManagerId, AdminRole.Manager)
            .PostAsJsonAsync(WriteOffRoute, new { receivableId = s.OwnId, note = "Goodwill after a complaint" });

        HttpAssert.IsOk(response);
        var written = await ReadAsync(s.OwnId);
        Assert.Equal(ReceivableStatus.WrittenOff, written.Status);
        Assert.Equal((ManagerId, "Goodwill after a complaint"), (written.WrittenOffByUserId, written.WriteOffNote));
        Assert.NotNull(written.WrittenOffOn);
    }

    [Fact]
    public async Task A_Receivable_Already_Written_Off_Or_Of_Another_Company_Is_Refused_And_Unchanged()
    {
        var s = await ArrangeAsync();
        var manager = Admin(ManagerId, AdminRole.Manager);

        await HttpAssert.AssertBusinessErrorAsync(
            await manager.PostAsJsonAsync(WriteOffRoute, new { receivableId = s.WrittenOffId, note = "Again" }),
            BusinessErrorMessage.ReceivableNotOpen);
        await HttpAssert.AssertBusinessErrorAsync(
            await manager.PostAsJsonAsync(WriteOffRoute, new { receivableId = s.OtherCompanysId, note = "Not ours" }),
            BusinessErrorMessage.ReceivableNotFound);

        Assert.Equal("earlier-admin", (await ReadAsync(s.WrittenOffId)).WrittenOffByUserId);
        Assert.Equal(ReceivableStatus.Open, (await ReadAsync(s.OtherCompanysId)).Status);
    }
}
