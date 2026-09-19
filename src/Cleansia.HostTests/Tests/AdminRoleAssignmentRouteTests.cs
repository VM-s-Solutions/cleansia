using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0066 D4/D5 on the real admin host: an Administrator assigns a role and the act is audited with
/// the role it ran under; a Support caller is refused the route at the gate; the caller cannot change
/// their own role; the company's last Administrator cannot be demoted or deactivated while only a
/// Support remains; every act by a Manager, a Support and an Accountant lands on the admin table with
/// their role; and a demoted administrator's next sign-in mints the new role — the hint in the body,
/// the claim in the cookie — and that token is refused an Administrator-only route.
/// </summary>
public sealed class AdminRoleAssignmentRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Password = "12345678Test!";
    private const string AdministratorId = "role-assign-admin";
    private const string AdministratorEmail = "role-assign-admin@hosttests.local";
    private const string SecondAdministratorId = "role-assign-admin-2";
    private const string SecondAdministratorEmail = "role-assign-admin-2@hosttests.local";
    private const string ManagerId = "role-assign-manager";
    private const string ManagerEmail = "role-assign-manager@hosttests.local";
    private const string SupportId = "role-assign-support";
    private const string SupportEmail = "role-assign-support@hosttests.local";
    private const string AccountantId = "role-assign-accountant";
    private const string AccountantEmail = "role-assign-accountant@hosttests.local";
    private const string CustomerId = "role-assign-customer";
    private const string CustomerEmail = "role-assign-customer@hosttests.local";

    private Task SeedAsync(bool secondAdministrator = false) =>
        SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            ctx.Users.Add(Admin(AdministratorId, AdministratorEmail, AdminRole.Administrator));
            ctx.Users.Add(Admin(ManagerId, ManagerEmail, AdminRole.Manager));
            ctx.Users.Add(Admin(SupportId, SupportEmail, AdminRole.Support));
            ctx.Users.Add(Admin(AccountantId, AccountantEmail, AdminRole.Accountant));
            if (secondAdministrator)
            {
                ctx.Users.Add(Admin(SecondAdministratorId, SecondAdministratorEmail, AdminRole.Administrator));
            }

            var customer = DomainSeed.Customer(CustomerEmail);
            customer.Id = CustomerId;
            ctx.Users.Add(customer);
        });

    private static Core.Domain.Users.User Admin(string id, string email, AdminRole role)
    {
        var user = DomainSeed.Admin(email, role: role);
        user.Id = id;
        return user;
    }

    private HttpClient As(string id, string email, AdminRole role) =>
        AdminClient(TestJwtFactory.Mint(AdminAudience, id, email, UserProfile.Administrator, adminRole: role));

    private static Task<HttpResponseMessage> SetRoleAsync(HttpClient client, string userId, AdminRole role) =>
        client.PostAsJsonAsync($"/api/AdminUser/{userId}/role", new { userId, role = (int)role });

    private Task<AdminRole?> RoleOfAsync(string userId) =>
        QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.AdminRole).SingleAsync());

    [Fact]
    public async Task An_administrator_assigns_a_role_and_the_act_is_audited_with_the_role_it_ran_under()
    {
        await SeedAsync();

        var response = await SetRoleAsync(As(AdministratorId, AdministratorEmail, AdminRole.Administrator), SupportId, AdminRole.Accountant);

        HttpAssert.IsOk(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(SupportId, body.RootElement.GetProperty("id").GetString());
        Assert.Equal((int)AdminRole.Accountant, body.RootElement.GetProperty("role").GetInt32());
        Assert.Equal(AdminRole.Accountant, await RoleOfAsync(SupportId));

        var audit = await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().SingleAsync(a => a.Action == "admin.user.set_role"));
        Assert.True(audit.Success);
        Assert.Equal(AdministratorId, audit.ActorId);
        Assert.Equal(UserProfile.Administrator, audit.ActorProfile);
        Assert.Equal(AdminRole.Administrator, audit.ActorAdminRole);
        Assert.Equal("AdminUser", audit.ResourceType);
        Assert.Equal(SupportId, audit.ResourceId);
        Assert.Contains("\"support\"", audit.BeforeJson);
        Assert.Contains("\"accountant\"", audit.AfterJson);
        Assert.DoesNotContain("@", audit.BeforeJson + audit.AfterJson);
    }

    [Fact]
    public async Task The_list_and_the_detail_carry_the_role()
    {
        await SeedAsync();
        var client = As(AdministratorId, AdministratorEmail, AdminRole.Administrator);

        var detail = await client.GetAsync($"/api/AdminUser/details/{AccountantId}");
        HttpAssert.IsOk(detail);
        using var detailBody = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal((int)AdminRole.Accountant, detailBody.RootElement.GetProperty("adminRole").GetInt32());

        var list = await client.GetAsync("/api/AdminUser/get-paged");
        HttpAssert.IsOk(list);
        using var listBody = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var rows = listBody.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(4, rows.Count);
        Assert.Contains(rows, r => r.GetProperty("id").GetString() == ManagerId && r.GetProperty("adminRole").GetInt32() == (int)AdminRole.Manager);
    }

    [Fact]
    public async Task A_support_caller_is_refused_the_route_at_the_gate()
    {
        await SeedAsync();

        var response = await SetRoleAsync(As(SupportId, SupportEmail, AdminRole.Support), AccountantId, AdminRole.Support);

        HttpAssert.IsForbidden(response);
        Assert.Equal(AdminRole.Accountant, await RoleOfAsync(AccountantId));
    }

    [Fact]
    public async Task The_caller_cannot_change_their_own_role()
    {
        await SeedAsync(secondAdministrator: true);

        var response = await SetRoleAsync(As(AdministratorId, AdministratorEmail, AdminRole.Administrator), AdministratorId, AdminRole.Support);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(response, BusinessErrorMessage.CannotChangeOwnRole);
        Assert.Equal(AdminRole.Administrator, await RoleOfAsync(AdministratorId));
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_demoted_while_only_a_support_remains()
    {
        // A token an Administrator held before being demoted still says Administrator for its lifetime;
        // it is the database guard, not the gate, that keeps the company from losing its last one.
        await SeedAsync();
        await QueryAsync(async ctx =>
        {
            await ctx.Users.IgnoreQueryFilters().Where(u => u.Id == SupportId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.AdminRole, AdminRole.Administrator));
            await ctx.Users.IgnoreQueryFilters().Where(u => u.Id == AdministratorId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.AdminRole, AdminRole.Support));
            return 0;
        });

        var response = await SetRoleAsync(As(AdministratorId, AdministratorEmail, AdminRole.Administrator), SupportId, AdminRole.Support);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(response, BusinessErrorMessage.CannotDemoteLastAdministrator);
        Assert.Equal(AdminRole.Administrator, await RoleOfAsync(SupportId));

        var audit = await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().SingleAsync(a => a.Action == "admin.user.set_role"));
        Assert.False(audit.Success);
        Assert.Equal(BusinessErrorMessage.CannotDemoteLastAdministrator, audit.ErrorCode);
        Assert.Equal(AdminRole.Administrator, audit.ActorAdminRole);
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_deactivated_while_only_a_support_remains()
    {
        await SeedAsync();
        await QueryAsync(async ctx =>
        {
            await ctx.Users.IgnoreQueryFilters().Where(u => u.Id == SupportId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.AdminRole, AdminRole.Administrator));
            await ctx.Users.IgnoreQueryFilters().Where(u => u.Id == AdministratorId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.AdminRole, AdminRole.Support));
            return 0;
        });

        var response = await As(AdministratorId, AdministratorEmail, AdminRole.Administrator)
            .PostAsync($"/api/AdminUser/{SupportId}/deactivate", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await HttpAssert.AssertBusinessErrorAsync(response, BusinessErrorMessage.CannotDeactivateLastAdmin);
        var target = await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SupportId));
        Assert.True(target.IsActive);
    }

    [Fact]
    public async Task With_two_administrators_one_can_be_demoted_and_the_other_deactivated_a_support()
    {
        await SeedAsync(secondAdministrator: true);
        var client = As(AdministratorId, AdministratorEmail, AdminRole.Administrator);

        HttpAssert.IsOk(await SetRoleAsync(client, SecondAdministratorId, AdminRole.Manager));
        Assert.Equal(AdminRole.Manager, await RoleOfAsync(SecondAdministratorId));

        HttpAssert.IsOk(await client.PostAsync($"/api/AdminUser/{SupportId}/deactivate", content: null));
        var support = await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SupportId));
        Assert.False(support.IsActive);
        Assert.Equal(AdministratorId, support.DeactivatedBy);
    }

    [Fact]
    public async Task Every_act_by_a_manager_a_support_and_an_accountant_is_audited_with_their_role()
    {
        await SeedAsync();

        HttpAssert.IsOk(await As(ManagerId, ManagerEmail, AdminRole.Manager).PostAsJsonAsync(
            "/api/AdminCredit/expire",
            new { userId = CustomerId, currencyId = DomainSeed.CurrencyId, note = "leaving", requestId = "role-assign-expire-1" }));
        HttpAssert.IsOk(await As(SupportId, SupportEmail, AdminRole.Support).PostAsync(
            $"/api/v1/AdminGdpr/export/{CustomerId}", content: null));
        HttpAssert.IsOk(await As(AccountantId, AccountantEmail, AdminRole.Accountant).PostAsJsonAsync(
            "/api/AdminPayPeriod/create",
            new { startDate = "2026-03-01", endDate = "2026-03-15", notes = (string?)null }));

        var rows = await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().Where(a => a.Success).ToListAsync());
        Assert.Equal(AdminRole.Manager, Assert.Single(rows, r => r.ActorId == ManagerId).ActorAdminRole);
        Assert.Equal(AdminRole.Support, Assert.Single(rows, r => r.ActorId == SupportId).ActorAdminRole);
        Assert.Equal(AdminRole.Accountant, Assert.Single(rows, r => r.ActorId == AccountantId).ActorAdminRole);
        Assert.All(rows, r => Assert.Equal(UserProfile.Administrator, r.ActorProfile));
    }

    [Fact]
    public async Task A_demoted_administrator_next_sign_in_mints_the_new_role_and_that_token_is_refused_the_administrator_routes()
    {
        await SeedAsync(secondAdministrator: true);
        HttpAssert.IsOk(await SetRoleAsync(As(AdministratorId, AdministratorEmail, AdminRole.Administrator), SecondAdministratorId, AdminRole.Support));

        var login = await AdminClientAnonymous().PostAsJsonAsync("/api/AdminAuth/Login",
            new { email = SecondAdministratorEmail, password = Password, rememberMe = true });

        HttpAssert.IsOk(login);
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.Equal("Administrator", body.RootElement.GetProperty("role").GetString());
        Assert.Equal("Support", body.RootElement.GetProperty("adminRole").GetString());

        var accessCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("admin_token=", StringComparison.Ordinal));
        var accessToken = Uri.UnescapeDataString(accessCookie["admin_token=".Length..].Split(';')[0]);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Equal("Support", Assert.Single(jwt.Claims, c => c.Type == TestJwtFactory.AdminRoleClaimType).Value);

        var demoted = AdminClient(accessToken);
        HttpAssert.IsForbidden(await demoted.GetAsync("/api/AdminCompanyLifecycle/get"));
        HttpAssert.IsForbidden(await SetRoleAsync(demoted, AdministratorId, AdminRole.Support));
        HttpAssert.IsOk(await demoted.GetAsync("/api/AdminAuditLog/get-paged"));
    }
}
