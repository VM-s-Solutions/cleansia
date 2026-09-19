using System.Net;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0066 D9.3, the Accountant token on the real admin host: it reads the invoice list, the pay
/// periods, the revenue report, a cleaner's masked payout details and the catalogue; it is refused
/// the order detail, the customer page and an employee document download — the PII reads the eight
/// admin-host constants exist to gate — and the Manager's and the Administrator's areas.
/// </summary>
public sealed class AdminRoleAccountantBehaviourTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string AccountantId = "role-accountant-1";
    private const string AccountantEmail = "role-accountant-1@hosttests.local";
    private const string CustomerId = "role-accountant-customer";
    private const string CustomerEmail = "role-accountant-customer@hosttests.local";
    private const string CleanerEmail = "role-accountant-cleaner@hosttests.local";

    private async Task<(string OrderId, string EmployeeId, string DocumentId)> SeedAsync()
    {
        string orderId = "", employeeId = "", documentId = "";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var accountant = DomainSeed.Admin(AccountantEmail, role: AdminRole.Accountant);
            accountant.Id = AccountantId;
            ctx.Users.Add(accountant);
            var customer = DomainSeed.Customer(CustomerEmail);
            customer.Id = CustomerId;
            ctx.Users.Add(customer);
            var order = DomainSeed.NewOrder(customer.Id, customer.Email);
            ctx.Orders.Add(order);
            var cleanerUser = DomainSeed.EmployeeUser(CleanerEmail);
            ctx.Users.Add(cleanerUser);
            var cleaner = DomainSeed.ApprovedEmployee(cleanerUser);
            ctx.Employees.Add(cleaner);
            var document = DomainSeed.ActiveDocument(cleaner.Id);
            ctx.EmployeeDocuments.Add(document);
            orderId = order.Id;
            employeeId = cleaner.Id;
            documentId = document.Id;
        });
        return (orderId, employeeId, documentId);
    }

    private HttpClient Accountant() =>
        AdminClient(TestJwtFactory.Mint(AdminAudience, AccountantId, AccountantEmail, UserProfile.Administrator, adminRole: AdminRole.Accountant));

    [Fact]
    public async Task Reads_the_invoice_list_the_pay_periods_the_revenue_report_the_masked_payout_details_and_the_catalogue()
    {
        var (_, employeeId, _) = await SeedAsync();
        var client = Accountant();

        HttpAssert.IsOk(await client.GetAsync("/api/AdminInvoice/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminPayPeriod/get-paged"));
        HttpAssert.IsOk(await client.GetAsync($"/api/AdminReport/revenue?startDate=2026-01-01&endDate=2026-01-31&currencyId={DomainSeed.CurrencyId}"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminPayConfig/get-paged"));
        // The masked read is the Accountant's; its body depends on the cleaner's payout record, so only the gate is asserted here.
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/AdminEmployee/{employeeId}/payout-details")).StatusCode);
        HttpAssert.IsOk(await client.GetAsync("/api/AdminEmployee/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminService/get-paged"));
        HttpAssert.IsOk(await client.GetAsync("/api/AdminNotification/unread-count"));
    }

    [Fact]
    public async Task Is_refused_the_order_detail_the_customer_page_and_an_employee_document_download()
    {
        var (orderId, employeeId, documentId) = await SeedAsync();
        var client = Accountant();

        HttpAssert.IsForbidden(await client.GetAsync($"/api/AdminOrder/details/{orderId}"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminOrder/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync($"/api/AdminOrder/{orderId}/customer"));
        HttpAssert.IsForbidden(await client.GetAsync($"/api/AdminUser/{CustomerId}"));
        HttpAssert.IsForbidden(await client.GetAsync($"/api/AdminEmployeeDocument/{documentId}/download"));
        HttpAssert.IsForbidden(await client.PostAsync($"/api/AdminEmployee/{employeeId}/payout-details/reveal", content: null));
        HttpAssert.IsForbidden(await client.GetAsync("/api/CustomerAudit/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminDispute/get-paged"));
    }

    [Fact]
    public async Task Is_refused_the_manager_and_administrator_areas()
    {
        await SeedAsync();
        var client = Accountant();

        HttpAssert.IsForbidden(await client.PostAsync("/api/AdminPayConfig/create", content: null));
        HttpAssert.IsForbidden(await client.PutAsync("/api/AdminService/update/some-service", content: null));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminUser/get-paged"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminCompanyLifecycle/get"));
        HttpAssert.IsForbidden(await client.GetAsync("/api/AdminLegal/get-versions"));
        HttpAssert.IsForbidden(await client.PostAsync($"/api/AdminUser/{AccountantId}/role", content: null));
    }
}
