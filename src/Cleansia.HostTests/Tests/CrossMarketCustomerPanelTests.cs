using System.Net;
using System.Text.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

public sealed class CrossMarketCustomerPanelTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private async Task<(string CustomerId, string OrderId)> SeedCustomerAsync(string accountTenant, string orderTenant)
    {
        string customerId = "", orderId = "";
        await SeedAsync(async context =>
        {
            await DomainSeed.EnsureReferenceDataAsync(context);
            var customer = DomainSeed.Customer("traveller@hosttests.local", accountTenant);
            customer.Update("Travelling", "PrivateSurname", "+420777222444", new DateOnly(1991, 2, 3));
            context.Users.Add(customer);
            var order = DomainSeed.NewOrder(customer.Id, customer.Email, tenantId: orderTenant);
            context.Orders.Add(order);
            customerId = customer.Id;
            orderId = order.Id;
        });
        return (customerId, orderId);
    }

    [Theory]
    [InlineData(HostTestTenants.A, HostTestTenants.B)]
    [InlineData(HostTestTenants.B, HostTestTenants.A)]
    public async Task Operator_admin_reads_only_the_masked_customer_panel_for_their_order(string accountTenant, string operatorTenant)
    {
        var world = await SeedCustomerAsync(accountTenant, operatorTenant);
        var token = TestJwtFactory.Mint(AdminAudience, "admin", "admin@hosttests.local", UserProfile.Administrator, tenantId: operatorTenant);
        var response = await AdminClient(token).GetAsync($"/api/AdminOrder/{world.OrderId}/customer");
        HttpAssert.IsOk(response);
        var json = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(json);
        var panel = body.RootElement.GetProperty("customerOfAnotherCompany");
        Assert.Equal(world.CustomerId, panel.GetProperty("id").GetString());
        Assert.Equal("Travelling", panel.GetProperty("firstName").GetString());
        Assert.Equal("t***@hosttests.local", panel.GetProperty("maskedEmail").GetString());
        Assert.False(string.IsNullOrEmpty(panel.GetProperty("companyName").GetString()));
        Assert.DoesNotContain("PrivateSurname", json);
        Assert.DoesNotContain("traveller@", json);
        Assert.DoesNotContain("777222444", json);
        Assert.DoesNotContain("1991", json);

        var detail = await AdminClient(token).GetAsync($"/api/AdminOrder/details/{world.OrderId}");
        HttpAssert.IsOk(detail);
        using var detailBody = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal(panel.GetProperty("companyName").GetString(), detailBody.RootElement.GetProperty("customerCompany").GetString());
    }

    [Fact]
    public async Task Cross_company_customer_is_unreachable_without_an_operator_order()
    {
        var world = await SeedCustomerAsync(HostTestTenants.A, HostTestTenants.B);
        var token = TestJwtFactory.Mint(AdminAudience, "admin", "admin@hosttests.local", UserProfile.Administrator, tenantId: HostTestTenants.B);
        var response = await AdminClient(token).GetAsync("/api/AdminOrder/unrelated-order/customer");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(world.CustomerId, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Customer_cannot_call_the_admin_panel_even_for_their_own_account()
    {
        var world = await SeedCustomerAsync(HostTestTenants.A, HostTestTenants.B);
        var token = TestJwtFactory.Mint(AdminAudience, world.CustomerId, "traveller@hosttests.local", UserProfile.Customer, tenantId: HostTestTenants.A);
        var response = await AdminClient(token).GetAsync($"/api/AdminOrder/{world.OrderId}/customer");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Fact]
    public async Task Same_company_order_customer_resolves_the_existing_customer_record()
    {
        var world = await SeedCustomerAsync(HostTestTenants.A, HostTestTenants.A);
        var token = TestJwtFactory.Mint(AdminAudience, "admin", "admin@hosttests.local", UserProfile.Administrator, tenantId: HostTestTenants.A);
        var response = await AdminClient(token).GetAsync($"/api/AdminOrder/{world.OrderId}/customer");
        HttpAssert.IsOk(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(world.CustomerId, body.RootElement.GetProperty("id").GetString());
        Assert.Equal("traveller@hosttests.local", body.RootElement.GetProperty("email").GetString());
        Assert.Equal("PrivateSurname", body.RootElement.GetProperty("lastName").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("customerOfAnotherCompany").ValueKind);
    }

    [Fact]
    public async Task An_order_in_another_company_cannot_resolve_its_customer()
    {
        var world = await SeedCustomerAsync(HostTestTenants.A, HostTestTenants.B);
        var token = TestJwtFactory.Mint(AdminAudience, "admin", "admin@hosttests.local", UserProfile.Administrator, tenantId: HostTestTenants.A);
        var response = await AdminClient(token).GetAsync($"/api/AdminOrder/{world.OrderId}/customer");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("traveller@", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("GET", "/api/AdminUser/get-paged")]
    [InlineData("GET", "/api/AdminUser/details/administrator")]
    [InlineData("POST", "/api/AdminUser/create")]
    [InlineData("PUT", "/api/AdminUser/update/administrator")]
    [InlineData("POST", "/api/AdminUser/administrator/deactivate")]
    [InlineData("POST", "/api/AdminUser/administrator/activate")]
    public async Task Existing_admin_user_routes_remain_mapped_and_protected(string method, string route)
    {
        var token = TestJwtFactory.Mint(AdminAudience, "customer", "customer@hosttests.local", UserProfile.Customer, tenantId: HostTestTenants.A);
        var response = await AdminClient(token).SendAsync(new HttpRequestMessage(new HttpMethod(method), route));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
