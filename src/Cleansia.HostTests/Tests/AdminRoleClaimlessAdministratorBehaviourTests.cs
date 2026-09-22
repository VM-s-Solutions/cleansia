using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0066 D2/D9.3, the token production no longer mints: an Administrator-profile token carrying no
/// <c>admin_role</c> claim, the session opened before the deploy and not yet refreshed. It is admitted
/// on every any-administrator route — the notifications feed, the catalogue reads, the cleaner list —
/// and refused every set-mapped route, whichever branch of the lattice, until its refresh carries the
/// claim. Beside it, the other shape production never mints: an Employee on the admin audience is
/// refused everywhere.
/// </summary>
public sealed class AdminRoleClaimlessAdministratorBehaviourTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string AdminId = "role-claimless-1";
    private const string AdminEmail = "role-claimless-1@hosttests.local";

    private Task SeedAsync() =>
        SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var admin = DomainSeed.Admin(AdminEmail);
            admin.Id = AdminId;
            ctx.Users.Add(admin);
        });

    private HttpClient Claimless() =>
        AdminClient(TestJwtFactory.Mint(AdminAudience, AdminId, AdminEmail, UserProfile.Administrator, claimlessAdministrator: true));

    [Fact]
    public async Task Is_admitted_on_the_any_administrator_routes()
    {
        await SeedAsync();
        var client = Claimless();

        HttpAssert.IsOk(await client.GetAsync("/api/AdminNotification/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminNotification/unread-count"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminService/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminEmployee/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminCode/GetOverview"));
    }

    [Fact]
    public async Task Is_refused_every_set_mapped_route()
    {
        await SeedAsync();
        var client = Claimless();

        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminAuditLog/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminOrder/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminInvoice/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminPayPeriod/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminUser/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminCompanyLifecycle/get"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminLegal/get-versions"));
        HttpAssert.IsForbidden(await client.PostAsync($"/api/AdminUser/{AdminId}/role", content: null));
    }

    [Fact]
    public async Task An_employee_on_the_admin_audience_is_refused_everywhere()
    {
        var employee = AdminClient(TestJwtFactory.Mint(AdminAudience, "emp-1", "emp-1@hosttests.local", UserProfile.Employee));

        HttpAssert.IsForbidden(await employee.GetAsync("/api/AdminNotification/unread-count"));
        HttpAssert.IsForbidden(await employee.GetAsync("/api/AdminService/get-paged"));
        HttpAssert.IsForbidden(await employee.GetAsync("/api/AdminAuditLog/get-paged"));
        HttpAssert.IsForbidden(await employee.GetAsync("/api/AdminInvoice/get-paged"));
    }
}
