using System.Net;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0066 D9.3, the Manager token on the real admin host: everything but the company lifecycle,
/// the company settings, the legal documents and the administrator accounts. It reads both the
/// Support's and the Accountant's areas and clears the catalogue writes; it is refused the four
/// Administrator areas and role assignment.
/// </summary>
public sealed class AdminRoleManagerBehaviourTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string ManagerId = "role-manager-1";
    private const string ManagerEmail = "role-manager-1@hosttests.local";
    private const string CustomerId = "role-manager-customer";
    private const string CustomerEmail = "role-manager-customer@hosttests.local";

    private async Task<string> SeedAsync()
    {
        var orderId = string.Empty;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var manager = DomainSeed.Admin(ManagerEmail, role: AdminRole.Manager);
            manager.Id = ManagerId;
            ctx.Users.Add(manager);
            var customer = DomainSeed.Customer(CustomerEmail);
            customer.Id = CustomerId;
            ctx.Users.Add(customer);
            var order = DomainSeed.NewOrder(customer.Id, customer.Email);
            ctx.Orders.Add(order);
            orderId = order.Id;
        });
        return orderId;
    }

    private HttpClient Manager() =>
        AdminClient(TestJwtFactory.Mint(AdminAudience, ManagerId, ManagerEmail, UserProfile.Administrator, adminRole: AdminRole.Manager));

    [Fact]
    public async Task Reads_both_branches_of_the_lattice_and_the_administrator_list()
    {
        var orderId = await SeedAsync();
        var client = Manager();

        HttpAssert.IsOk(await client.GetAsync($"/api/AdminOrder/details/{orderId}"));
        HttpAssert.IsOk(await client.GetAsync("/api/CustomerAudit/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminInvoice/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminPayPeriod/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminUser/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminService/get-paged"));
    }

    [Fact]
    public async Task Clears_the_gate_on_the_manager_writes()
    {
        await SeedAsync();
        var client = Manager();

        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.PutAsync("/api/AdminService/update/some-service", content: null)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.PostAsync("/api/AdminPayConfig/create", content: null)).StatusCode);
        // The erasure gate, on a request id that names nothing: the gate is cleared and the validator answers.
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/AdminGdpr/requests/no-such-request/retry-deletion", content: null)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.PostAsync("/api/AdminCredit/expire", content: null)).StatusCode);
    }

    [Fact]
    public async Task Is_refused_the_company_lifecycle_the_settings_the_legal_documents_and_the_accounts()
    {
        await SeedAsync();
        var client = Manager();

        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminCompanyLifecycle/get"));
        HttpAssert.IsForbidden(await client.PostAsync("/api/AdminCompanyLifecycle/deactivate", content: null));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminTenantSettings/get-all"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminLegal/get-versions"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminLegal/get-document/any-id"));
        HttpAssert.IsForbidden(await client.PostAsync("/api/AdminUser/create", content: null));
        HttpAssert.IsForbidden(await client.PostAsync($"/api/AdminUser/{ManagerId}/deactivate", content: null));
        HttpAssert.IsForbidden(await client.PostAsync($"/api/AdminUser/{ManagerId}/role", content: null));
    }
}
