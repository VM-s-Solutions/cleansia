using System.Text.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The admin customer list: Support and above page through their own company's customers and search
/// them on the real Postgres translation; an Accountant is refused, and no cleaner, administrator or
/// other company's customer is ever in the list.
/// </summary>
public sealed class AdminCustomerRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string GetPagedRoute = "/api/AdminCustomer/get-paged";
    private const string SupportId = "customers-support";
    private const string AccountantId = "customers-accountant";

    private sealed record Seeded(string WithPhoneId, string WithoutPhoneId);

    private async Task<Seeded> ArrangeAsync()
    {
        Seeded seeded = null!;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var support = DomainSeed.Admin($"{SupportId}@hosttests.local", role: AdminRole.Support);
            support.Id = SupportId;
            var accountant = DomainSeed.Admin($"{AccountantId}@hosttests.local", role: AdminRole.Accountant);
            accountant.Id = AccountantId;
            var withPhone = DomainSeed.Customer("with-phone@hosttests.local");
            withPhone.Update("Jana", "Novakova", "+420777123456");
            var withoutPhone = DomainSeed.Customer("without-phone@hosttests.local");
            var cleaner = DomainSeed.EmployeeUser("cleaner@hosttests.local");
            var otherCompanys = DomainSeed.Customer("other-company@hosttests.local", HostTestTenants.B);
            otherCompanys.Update("Jana", "Novakova", "+420777123456");
            ctx.Users.AddRange(support, accountant, withPhone, withoutPhone, cleaner, otherCompanys);
            seeded = new Seeded(withPhone.Id, withoutPhone.Id);
        });
        return seeded;
    }

    private HttpClient Admin(string userId, AdminRole role) =>
        AdminClient(TestJwtFactory.Mint(
            AdminAudience, userId, $"{userId}@hosttests.local", UserProfile.Administrator, adminRole: role));

    private static async Task<HashSet<string?>> IdsOf(HttpResponseMessage response)
    {
        HttpAssert.IsOk(response);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var ids = body.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToHashSet();
        Assert.Equal(ids.Count, body.GetProperty("total").GetInt32());
        return ids;
    }

    [Fact]
    public async Task Support_Lists_Only_Its_Own_Companys_Customers()
    {
        var s = await ArrangeAsync();

        var ids = await IdsOf(await Admin(SupportId, AdminRole.Support).GetAsync(GetPagedRoute));

        Assert.Equal(new HashSet<string?> { s.WithPhoneId, s.WithoutPhoneId }, ids);
    }

    [Fact]
    public async Task Support_Finds_A_Customer_By_Phone_And_By_Name()
    {
        var s = await ArrangeAsync();
        var support = Admin(SupportId, AdminRole.Support);

        Assert.Equal(new HashSet<string?> { s.WithPhoneId }, await IdsOf(await support.GetAsync($"{GetPagedRoute}?Filter.SearchTerm=777123")));
        Assert.Equal(new HashSet<string?> { s.WithPhoneId }, await IdsOf(await support.GetAsync($"{GetPagedRoute}?Filter.SearchTerm=NOVAK")));
    }

    [Fact]
    public async Task An_Accountant_Is_Refused()
    {
        await ArrangeAsync();

        HttpAssert.IsForbidden(await Admin(AccountantId, AdminRole.Accountant).GetAsync(GetPagedRoute));
    }
}
