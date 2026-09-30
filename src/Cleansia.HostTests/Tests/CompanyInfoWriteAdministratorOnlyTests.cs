using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The company's legal identity and bank account are printed on every receipt and cleaner invoice, so
/// only an Administrator may write them; every administrator may still read them.
/// </summary>
public sealed class CompanyInfoWriteAdministratorOnlyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string AdministratorId = "company-info-administrator";
    private const string AdministratorEmail = "company-info-administrator@hosttests.local";
    private const string ManagerId = "company-info-manager";
    private const string ManagerEmail = "company-info-manager@hosttests.local";

    private Task SeedAsync() =>
        SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var administrator = DomainSeed.Admin(AdministratorEmail, role: AdminRole.Administrator);
            administrator.Id = AdministratorId;
            ctx.Users.Add(administrator);
            var manager = DomainSeed.Admin(ManagerEmail, role: AdminRole.Manager);
            manager.Id = ManagerId;
            ctx.Users.Add(manager);
        });

    private HttpClient Client(string id, string email, AdminRole role) =>
        AdminClient(TestJwtFactory.Mint(AdminAudience, id, email, UserProfile.Administrator, adminRole: role));

    [Fact]
    public async Task A_manager_reads_the_company_info_but_cannot_write_it()
    {
        await SeedAsync();
        var manager = Client(ManagerId, ManagerEmail, AdminRole.Manager);

        HttpAssert.IsOk(await manager.GetAsync("/api/AdminCompany/get-paged"));
        HttpAssert.IsForbidden(await manager.PostAsync("/api/AdminCompany/create", content: null));
        HttpAssert.IsForbidden(await manager.PutAsync("/api/AdminCompany/update/some-company-info", content: null));
        HttpAssert.IsForbidden(await manager.DeleteAsync("/api/AdminCompany/delete/some-company-info"));
    }

    [Fact]
    public async Task An_administrator_clears_the_gate_on_every_write()
    {
        await SeedAsync();
        var administrator = Client(AdministratorId, AdministratorEmail, AdminRole.Administrator);

        HttpAssert.ClearedTheGate(await administrator.PostAsync("/api/AdminCompany/create", content: null));
        HttpAssert.ClearedTheGate(await administrator.PutAsync("/api/AdminCompany/update/some-company-info", content: null));
        HttpAssert.ClearedTheGate(await administrator.DeleteAsync("/api/AdminCompany/delete/some-company-info"));
    }
}
