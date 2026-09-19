using System.Net;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0066 D9.3, the Support token on the real admin host: it reads the customer trail, the admin
/// log, the order detail, the customer's export and incident file, and it is admitted on the order
/// operations the ruling gives Support (an override, a refund — the D3 table, not the first draft);
/// it is refused an erasure and a service update (the Manager's), the invoice list and the pay
/// periods (the Accountant's), and the company lifecycle, the legal documents and an administrator
/// account (the Administrator's).
/// </summary>
public sealed class AdminRoleSupportBehaviourTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string SupportId = "role-support-1";
    private const string SupportEmail = "role-support-1@hosttests.local";
    private const string CustomerId = "role-support-customer";
    private const string CustomerEmail = "role-support-customer@hosttests.local";

    private async Task<string> SeedAsync()
    {
        var orderId = string.Empty;
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var support = DomainSeed.Admin(SupportEmail, role: AdminRole.Support);
            support.Id = SupportId;
            ctx.Users.Add(support);
            var customer = DomainSeed.Customer(CustomerEmail);
            customer.Id = CustomerId;
            ctx.Users.Add(customer);
            var order = DomainSeed.NewOrder(customer.Id, customer.Email);
            ctx.Orders.Add(order);
            ctx.CustomerActionAudits.Add(DomainSeed.CustomerAudit("caud-role-support-1", CustomerId, HostTestTenants.Default));
            orderId = order.Id;
        });
        return orderId;
    }

    private HttpClient Support() =>
        AdminClient(TestJwtFactory.Mint(AdminAudience, SupportId, SupportEmail, UserProfile.Administrator, adminRole: AdminRole.Support));

    [Fact]
    public async Task Reads_the_customer_trail_the_admin_log_the_order_detail_the_export_and_the_incident_file()
    {
        var orderId = await SeedAsync();
        var client = Support();

        HttpAssert.IsOk(await client.GetAsync("/api/CustomerAudit/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminAuditLog/get-paged"));
        HttpAssert.IsOk(await client.GetAsync($"/api/AdminOrder/details/{orderId}"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminOrder/get-paged"));
        HttpAssert.IsOk(await client.PostAsync($"/api/v1/AdminGdpr/export/{CustomerId}", content: null));
        HttpAssert.IsOk(await client.PostAsync($"/api/v1/AdminGdpr/incident-file/{CustomerId}", content: null));
    }

    [Fact]
    public async Task Clears_the_gate_on_the_order_operations_the_ruling_gives_support()
    {
        await SeedAsync();
        var client = Support();

        // Bodiless posts: the gate answers 403 before binding; anything else means the gate was cleared.
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.PostAsync("/api/AdminOrder/override-status", content: null)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.PostAsync("/api/AdminOrder/refund", content: null)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.PostAsync("/api/AdminRefund/partial", content: null)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.PostAsync("/api/AdminOrder/cancel", content: null)).StatusCode);
    }

    [Fact]
    public async Task Is_refused_the_manager_accountant_and_administrator_areas()
    {
        await SeedAsync();
        var client = Support();

        HttpAssert.IsForbidden(await client.PostAsync($"/api/v1/AdminGdpr/delete-account/{CustomerId}", content: null));
        HttpAssert.IsForbidden(await client.PutAsync("/api/AdminService/update/some-service", content: null));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminInvoice/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminPayPeriod/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminReport/revenue?startDate=2026-01-01&endDate=2026-01-31"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminCompanyLifecycle/get"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminLegal/get-versions"));
        HttpAssert.IsForbidden(await client.PostAsync("/api/AdminUser/create", content: null));
        HttpAssert.IsForbidden(await client.PostAsync($"/api/AdminUser/{SupportId}/role", content: null));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminTenantSettings/get-all"));
    }
}
