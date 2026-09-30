using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The company's cash in its cleaners' hands, on the real hosts (owner ruling 2026-09-28, decision 23): the
/// cash a cleaner records at the door is cash they hold, which they read on either partner host; an
/// Accountant reads what every cleaner of their own company holds and records what is handed back, a
/// Manager writes the rest off with a note, and neither may take off more than the cleaner holds.
/// </summary>
public sealed class CashHeldRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string MarkCashRoute = "/api/Order/MarkCashCollected";
    private const string MyCashRoute = "/api/EmployeePayroll/GetCashHeld";
    private const string AdminListRoute = "/api/AdminCashHeld/get-all";
    private const string RemittanceRoute = "/api/AdminCashHeld/record-remittance";
    private const string WriteOffRoute = "/api/AdminCashHeld/write-off";
    private const string CleanerUserId = "u-cash-cleaner";
    private const string CleanerEmail = "cash-cleaner@hosttests.local";

    private sealed record Arranged(string CleanerId, string InProgressOrderId, string OtherCompanysCleanerId);

    /// <summary>
    /// The cleaner holds 1500 from one earlier collection and has a cash clean in progress; a cleaner of
    /// another company holds 700.
    /// </summary>
    private async Task<Arranged> ArrangeAsync()
    {
        Arranged arranged = null!;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var customer = DomainSeed.Customer("cash-customer@hosttests.local");
            var cleanerUser = DomainSeed.EmployeeUser(CleanerEmail);
            var otherUser = DomainSeed.EmployeeUser("cash-other@hosttests.local", HostTestTenants.B);
            ctx.Users.AddRange(customer, cleanerUser, otherUser);
            var cleaner = DomainSeed.ApprovedEmployee(cleanerUser);
            var other = DomainSeed.ApprovedEmployee(otherUser, HostTestTenants.B);
            other.TenantId = HostTestTenants.B;
            ctx.Employees.AddRange(cleaner, other);
            ctx.EmployeeDocuments.Add(DomainSeed.ActiveDocument(cleaner.Id));

            CollectedBy(DomainSeed.NewOrder(customer.Id, customer.Email), cleaner, ctx);
            CollectedBy(DomainSeed.NewOrder(customer.Id, customer.Email, HostTestTenants.B), other, ctx, 700m);

            var inProgress = DomainSeed.NewOrder(
                customer.Id, customer.Email, HostTestTenants.Default, cleaningDateTime: DateTime.UtcNow.AddMinutes(-30));
            DomainSeed.ConfirmAndAssign(inProgress, cleaner);
            var started = OrderStatusTrack.Create(OrderStatus.InProgress, inProgress);
            started.Created("seed", DateTimeOffset.UtcNow.AddMinutes(1));
            started.TenantId = inProgress.TenantId;
            inProgress.AddOrderStatus(started);
            ctx.Orders.Add(inProgress);

            arranged = new Arranged(cleaner.Id, inProgress.Id, other.Id);
        });
        return arranged;
    }

    private static void CollectedBy(
        Order order, Employee cleaner, Infra.Database.CleansiaDbContext ctx, decimal? amount = null)
    {
        order.MarkCashCollected(cleaner.Id, DateTime.UtcNow.AddDays(-2), amount);
        var entry = CashLedgerEntry.ForCollection(order);
        entry.TenantId = order.TenantId ?? HostTestTenants.Default;
        ctx.Orders.Add(order);
        ctx.CashLedgerEntries.Add(entry);
    }

    private HttpClient Admin(AdminRole role) =>
        AdminClient(TestJwtFactory.Mint(
            AdminAudience, $"u-cash-{role}", $"cash-{role}@hosttests.local".ToLowerInvariant(), UserProfile.Administrator, adminRole: role));

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        HttpAssert.IsOk(response);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static decimal HeldBy(JsonElement list, string employeeId) =>
        list.EnumerateArray()
            .Where(b => b.GetProperty("employeeId").GetString() == employeeId)
            .Sum(b => b.GetProperty("amount").GetDecimal());

    private static object Movement(string employeeId, decimal amount, string? note = "Handed over at the office") =>
        new { employeeId, currencyId = DomainSeed.CurrencyId, amount, note };

    [Fact]
    public async Task Anonymous_Is_401_And_A_Customer_Is_403_On_Every_Cash_Held_Route()
    {
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().GetAsync(AdminListRoute));
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().PostAsJsonAsync(RemittanceRoute, Movement("e", 1m)));
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().PostAsJsonAsync(WriteOffRoute, Movement("e", 1m)));
        HttpAssert.IsUnauthorized(await PartnerClientAnonymous().GetAsync(MyCashRoute));
        HttpAssert.IsUnauthorized(await MobileClientAnonymous().GetAsync(MyCashRoute));

        var admin = AdminClient(TestJwtFactory.Mint(AdminAudience, "u-cust", "cust@hosttests.local", UserProfile.Customer));
        var partner = PartnerClient(TestJwtFactory.Mint(PartnerAudience, "u-cust", "cust@hosttests.local", UserProfile.Customer));
        HttpAssert.IsForbidden(await admin.GetAsync(AdminListRoute));
        HttpAssert.IsForbidden(await admin.PostAsJsonAsync(RemittanceRoute, Movement("e", 1m)));
        HttpAssert.IsForbidden(await admin.PostAsJsonAsync(WriteOffRoute, Movement("e", 1m)));
        HttpAssert.IsForbidden(await partner.GetAsync(MyCashRoute));
    }

    [Fact]
    public async Task Cash_The_Cleaner_Records_At_The_Door_Is_Cash_They_Hold_On_Both_Partner_Hosts()
    {
        var a = await ArrangeAsync();
        var mobile = MobileClient(TestJwtFactory.Mint(
            MobileAudience, CleanerUserId, CleanerEmail, UserProfile.Employee, employeeId: a.CleanerId));
        var web = PartnerClient(TestJwtFactory.Mint(
            PartnerAudience, CleanerUserId, CleanerEmail, UserProfile.Employee, employeeId: a.CleanerId));

        HttpAssert.IsOk(await mobile.PostAsJsonAsync(MarkCashRoute, new { orderId = a.InProgressOrderId }));

        var entry = await QueryAsync(ctx => ctx.CashLedgerEntries.IgnoreQueryFilters()
            .SingleAsync(e => e.OrderId == a.InProgressOrderId));
        Assert.Equal(
            (a.CleanerId, CashLedgerEntryKind.Collection, 1500m, DomainSeed.CurrencyId, (string?)HostTestTenants.Default),
            (entry.EmployeeId, entry.Kind, entry.Amount, entry.CurrencyId, entry.TenantId));

        foreach (var client in new[] { web, mobile })
        {
            var mine = Assert.Single((await BodyAsync(await client.GetAsync(MyCashRoute))).EnumerateArray());
            Assert.Equal(("CZK", 3000m), (mine.GetProperty("currencyCode").GetString(), mine.GetProperty("amount").GetDecimal()));
            Assert.Equal(JsonValueKind.Null, mine.GetProperty("floatCap").ValueKind);
            Assert.False(mine.GetProperty("cashJobsHidden").GetBoolean());
        }
    }

    /// <summary>
    /// Owner ruling 2026-09-28, decision 25: above the company's cash float cap, "cash I hold" states the cap
    /// and that cash jobs are hidden from the cleaner.
    /// </summary>
    [Fact]
    public async Task Above_The_Companys_Float_Cap_Cash_I_Hold_States_The_Cap_And_That_Cash_Jobs_Are_Hidden()
    {
        var a = await ArrangeAsync();
        await SeedAsync(ctx =>
        {
            var cap = TenantConfiguration.Create("cash.float_cap", "1000", category: "cash");
            cap.TenantId = HostTestTenants.Default;
            ctx.TenantConfigurations.Add(cap);
            return Task.CompletedTask;
        });
        var web = PartnerClient(TestJwtFactory.Mint(
            PartnerAudience, CleanerUserId, CleanerEmail, UserProfile.Employee, employeeId: a.CleanerId));

        var mine = Assert.Single((await BodyAsync(await web.GetAsync(MyCashRoute))).EnumerateArray());

        Assert.Equal(
            (1500m, 1000m, true),
            (mine.GetProperty("amount").GetDecimal(), mine.GetProperty("floatCap").GetDecimal(), mine.GetProperty("cashJobsHidden").GetBoolean()));
    }

    [Fact]
    public async Task An_Accountant_Reads_The_Cash_Held_By_Their_Own_Companys_Cleaners_Only()
    {
        var a = await ArrangeAsync();

        var list = await BodyAsync(await Admin(AdminRole.Accountant).GetAsync(AdminListRoute));

        var held = Assert.Single(list.EnumerateArray());
        Assert.Equal(a.CleanerId, held.GetProperty("employeeId").GetString());
        Assert.Equal("Emp Loyee", held.GetProperty("employeeName").GetString());
        Assert.Equal(("CZK", 1500m), (held.GetProperty("currencyCode").GetString(), held.GetProperty("amount").GetDecimal()));
        Assert.Equal(0m, HeldBy(list, a.OtherCompanysCleanerId));
    }

    [Fact]
    public async Task Support_Neither_Reads_Nor_Records_And_The_Cash_Held_Is_Unchanged()
    {
        var a = await ArrangeAsync();
        var support = Admin(AdminRole.Support);

        HttpAssert.IsForbidden(await support.GetAsync(AdminListRoute));
        HttpAssert.IsForbidden(await support.PostAsJsonAsync(RemittanceRoute, Movement(a.CleanerId, 100m)));
        HttpAssert.IsForbidden(await support.PostAsJsonAsync(WriteOffRoute, Movement(a.CleanerId, 100m)));

        Assert.Equal(1500m, HeldBy(await BodyAsync(await Admin(AdminRole.Accountant).GetAsync(AdminListRoute)), a.CleanerId));
    }

    [Fact]
    public async Task An_Accountant_Records_A_Remittance_And_A_Manager_Writes_Off_The_Rest()
    {
        var a = await ArrangeAsync();
        var accountant = Admin(AdminRole.Accountant);
        var manager = Admin(AdminRole.Manager);

        HttpAssert.IsOk(await accountant.PostAsJsonAsync(RemittanceRoute, Movement(a.CleanerId, 1000m)));
        Assert.Equal(500m, HeldBy(await BodyAsync(await accountant.GetAsync(AdminListRoute)), a.CleanerId));

        HttpAssert.IsForbidden(await accountant.PostAsJsonAsync(WriteOffRoute, Movement(a.CleanerId, 500m, "Left the company")));
        HttpAssert.IsOk(await manager.PostAsJsonAsync(WriteOffRoute, Movement(a.CleanerId, 500m, "Left the company")));

        Assert.Empty((await BodyAsync(await accountant.GetAsync(AdminListRoute))).EnumerateArray());
        var writeOff = await QueryAsync(ctx => ctx.CashLedgerEntries.IgnoreQueryFilters()
            .SingleAsync(e => e.EmployeeId == a.CleanerId && e.Kind == CashLedgerEntryKind.WriteOff));
        Assert.Equal((-500m, (string?)"Left the company", "u-cash-Manager"), (writeOff.Amount, writeOff.Note, writeOff.CreatedBy));
    }

    [Fact]
    public async Task More_Than_The_Cleaner_Holds_Or_A_Cleaner_Of_Another_Company_Is_Refused_And_Nothing_Is_Entered()
    {
        var a = await ArrangeAsync();
        var manager = Admin(AdminRole.Manager);

        await HttpAssert.AssertBusinessErrorAsync(
            await manager.PostAsJsonAsync(RemittanceRoute, Movement(a.CleanerId, 1500.01m)),
            BusinessErrorMessage.CashHeldAmountExceedsBalance);
        await HttpAssert.AssertBusinessErrorAsync(
            await manager.PostAsJsonAsync(WriteOffRoute, Movement(a.OtherCompanysCleanerId, 100m, "Not ours")),
            BusinessErrorMessage.EmployeeNotFound);

        Assert.Equal(2, await QueryAsync(ctx => ctx.CashLedgerEntries.IgnoreQueryFilters().CountAsync()));
    }
}
