using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Q-AUD-L5 overruled, end to end on the real hosts with their real <c>IHostAudienceProvider</c>: a
/// sign-in on the Customer host leaves a <c>customer.session.login</c> row, refused or not, and a refused
/// one for an unknown address carries the key and nowhere the address (the in-memory test server has no
/// remote IP to record; the request context is proven on the pipeline tests); the
/// Partner host still routes the anonymous <c>POST api/Auth/Register</c> whose command carries the
/// customer marker, and a refusal there lands in no table — the host gate, not the marker, decides; an
/// Administrator's sign-out on the Admin host lands in the admin table only.
/// </summary>
public sealed class SessionAuditRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string CustomerId = "session-audit-customer";
    private const string CustomerEmail = "session-audit@hosttests.local";
    private const string UnknownEmail = "nobody-session-audit@hosttests.local";
    private const string Password = "12345678Test!";
    private const string AdminId = "session-audit-admin";
    private const string AdminEmail = "session-audit-admin@hosttests.local";

    private async Task SeedCustomerAsync()
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var customer = DomainSeed.Customer(CustomerEmail);
            customer.Id = CustomerId;
            ctx.Users.Add(customer);
        });
    }

    private Task<List<CustomerActionAudit>> CustomerRowsAsync() =>
        QueryAsync(ctx => ctx.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());

    private Task<List<AdminActionAudit>> AdminRowsAsync() =>
        QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().ToListAsync());

    private static void AssertCarriesNoAddress(CustomerActionAudit row, string email)
    {
        foreach (var value in new[] { row.UserId, row.ResourceId, row.ResourceType, row.ErrorCode, row.PayloadJson, row.DeviceLabel, row.DeviceId, row.CorrelationId })
        {
            Assert.DoesNotContain(email, value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_Customer_SignIn_On_The_Customer_Host_Leaves_One_Success_Row_Keyed_On_The_Account()
    {
        await SeedCustomerAsync();

        var response = await CustomerClientAnonymous().PostAsJsonAsync("/api/Auth/Login",
            new { email = CustomerEmail, password = Password, rememberMe = true });

        HttpAssert.IsOk(response);
        var row = Assert.Single(await CustomerRowsAsync());
        Assert.Equal("customer.session.login", row.Action);
        Assert.True(row.Success);
        Assert.Equal(CustomerId, row.UserId);
        Assert.Equal("User", row.ResourceType);
        Assert.Equal(CustomerId, row.ResourceId);
        Assert.Equal(CustomerAudience, row.ClientAudience);
        Assert.Equal(HostTestTenants.Default, row.TenantId);
        var payload = JsonDocument.Parse(row.PayloadJson!).RootElement;
        Assert.Equal("Password", payload.GetProperty("method").GetString());
        Assert.True(payload.GetProperty("rememberMe").GetBoolean());
        Assert.Equal(CustomerAudience, payload.GetProperty("clientAudience").GetString());
        AssertCarriesNoAddress(row, CustomerEmail);
        Assert.Empty(await AdminRowsAsync());
    }

    [Fact]
    public async Task A_Refused_SignIn_For_An_Unknown_Address_On_The_Customer_Host_Leaves_One_Failure_Row_Without_The_Address()
    {
        await SeedCustomerAsync();

        var response = await CustomerClientAnonymous().PostAsJsonAsync("/api/Auth/Login",
            new { email = UnknownEmail, password = Password, rememberMe = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var row = Assert.Single(await CustomerRowsAsync());
        Assert.Equal("customer.session.login", row.Action);
        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.NotExistingUserWithEmail, row.ErrorCode);
        Assert.Null(row.UserId);
        Assert.Null(row.ResourceId);
        Assert.Null(row.PayloadJson);
        Assert.Equal(CustomerAudience, row.ClientAudience);
        Assert.Equal(HostTestTenants.Default, row.TenantId);
        AssertCarriesNoAddress(row, UnknownEmail);
    }

    /// <summary>The known case: the customer marker on <c>Register.Command</c>, dispatched anonymously by the Partner host.</summary>
    [Fact]
    public async Task A_Refused_Anonymous_Registration_On_The_Partner_Host_Lands_In_No_Table()
    {
        await SeedCustomerAsync();

        var response = await PartnerClientAnonymous().PostAsJsonAsync("/api/Auth/Register",
            new { email = "not-an-address", password = "short", firstName = "", lastName = "", language = "en" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await CustomerRowsAsync());
        Assert.Empty(await AdminRowsAsync());
    }

    [Fact]
    public async Task The_Same_Refused_Registration_On_The_Customer_Host_Leaves_One_Failure_Row()
    {
        await SeedCustomerAsync();

        var response = await CustomerClientAnonymous().PostAsJsonAsync("/api/Auth/Register",
            new { email = "not-an-address", password = "short", firstName = "", lastName = "", language = "en" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var row = Assert.Single(await CustomerRowsAsync());
        Assert.Equal("customer.account.register", row.Action);
        Assert.False(row.Success);
        Assert.Null(row.UserId);
    }

    [Fact]
    public async Task A_Customers_SignOut_On_The_Customer_Host_Leaves_One_Row_Keyed_On_The_Session_User()
    {
        await SeedCustomerAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, CustomerId, CustomerEmail, UserProfile.Customer);

        var response = await CustomerClient(token).PostAsJsonAsync("/api/Auth/Logout", new { token = string.Empty });

        HttpAssert.IsOk(response);
        var row = Assert.Single(await CustomerRowsAsync());
        Assert.Equal("customer.session.logout", row.Action);
        Assert.True(row.Success);
        Assert.Equal(CustomerId, row.UserId);
        Assert.Equal(CustomerId, row.ResourceId);
        Assert.False(JsonDocument.Parse(row.PayloadJson!).RootElement.GetProperty("tokenPresented").GetBoolean());
        Assert.Empty(await AdminRowsAsync());
    }

    [Fact]
    public async Task An_Administrators_SignOut_On_The_Admin_Host_Lands_In_The_Admin_Table_Only()
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var admin = DomainSeed.Admin(AdminEmail);
            admin.Id = AdminId;
            ctx.Users.Add(admin);
        });
        var token = TestJwtFactory.Mint(AdminAudience, AdminId, AdminEmail, UserProfile.Administrator);

        var response = await AdminClient(token).PostAsJsonAsync("/api/AdminAuth/Logout", new { token = string.Empty });

        HttpAssert.IsOk(response);
        Assert.Empty(await CustomerRowsAsync());
        var row = Assert.Single(await AdminRowsAsync());
        Assert.Equal(AdminId, row.ActorId);
        Assert.True(row.Success);
        Assert.Equal("User", row.ResourceType);
        Assert.Equal(AdminId, row.ResourceId);
    }
}
