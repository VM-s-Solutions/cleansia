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
/// remote IP to record; the request context is proven on the pipeline tests); neither partner host
/// routes the anonymous <c>POST api/Auth/Register</c> any more (owner ruling 2026-09-15: a partner host
/// provisions no customer), so the customer-marked command they used to dispatch cannot be reached
/// there and its refusal has no table to land in. An administrator's session acts are admin acts (owner
/// ruling 2026-09-15): a sign-in on the Admin host leaves an <c>admin.session.login</c> row in the admin
/// table, keyed on the account — refused or not, and a refusal names the account only where the address
/// resolved one, never the address itself; a refusal on a second operator's administrator is stamped with
/// that company; a sign-out lands in the admin table only, under the marker's admin label.
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

    private static void AssertCarriesNoAddress(AdminActionAudit row)
    {
        foreach (var value in new[] { row.ActorId, row.ActorEmail, row.ResourceId, row.ResourceType, row.ErrorCode, row.Reason, row.BeforeJson, row.AfterJson, row.CorrelationId })
        {
            Assert.DoesNotContain("@", value ?? string.Empty);
        }
    }

    private Task SeedAdminAsync(string tenantId = HostTestTenants.Default) =>
        SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var admin = DomainSeed.Admin(AdminEmail, tenantId);
            admin.Id = AdminId;
            ctx.Users.Add(admin);
        });

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

    [Theory]
    [InlineData(PartnerAudience)]
    [InlineData(MobileAudience)]
    public async Task A_Partner_Host_No_Longer_Routes_An_Anonymous_Customer_Registration(string hostAudience)
    {
        await SeedCustomerAsync();
        var anonymous = hostAudience == MobileAudience ? MobileClientAnonymous() : PartnerClientAnonymous();

        var response = await anonymous.PostAsJsonAsync("/api/Auth/Register",
            new { email = "would-be-customer@hosttests.local", password = Password, firstName = "Would", lastName = "Be", language = "en", termsAccepted = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "would-be-customer@hosttests.local")));
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
    public async Task An_Administrators_SignOut_On_The_Admin_Host_Lands_In_The_Admin_Table_Only_Under_The_Admin_Label()
    {
        await SeedAdminAsync();
        var token = TestJwtFactory.Mint(AdminAudience, AdminId, AdminEmail, UserProfile.Administrator);

        var response = await AdminClient(token).PostAsJsonAsync("/api/AdminAuth/Logout", new { token = string.Empty });

        HttpAssert.IsOk(response);
        Assert.Empty(await CustomerRowsAsync());
        var row = Assert.Single(await AdminRowsAsync());
        Assert.Equal("admin.session.logout", row.Action);
        Assert.Equal(AdminId, row.ActorId);
        Assert.True(row.Success);
        Assert.Equal("User", row.ResourceType);
        Assert.Equal(AdminId, row.ResourceId);
        Assert.Equal(HostTestTenants.Default, row.TenantId);
    }

    [Fact]
    public async Task An_Administrators_SignIn_On_The_Admin_Host_Leaves_One_Admin_Success_Row_Keyed_On_The_Account()
    {
        await SeedAdminAsync();

        var response = await AdminClientAnonymous().PostAsJsonAsync("/api/AdminAuth/Login",
            new { email = AdminEmail, password = Password, rememberMe = true });

        HttpAssert.IsOk(response);
        Assert.Empty(await CustomerRowsAsync());
        var row = Assert.Single(await AdminRowsAsync());
        Assert.Equal("admin.session.login", row.Action);
        Assert.True(row.Success);
        Assert.Null(row.ErrorCode);
        Assert.Equal(AdminId, row.ActorId);
        Assert.Equal(UserProfile.Administrator, row.ActorProfile);
        Assert.Equal("User", row.ResourceType);
        Assert.Equal(AdminId, row.ResourceId);
        Assert.Equal(HostTestTenants.Default, row.TenantId);
        var evidence = JsonDocument.Parse(row.AfterJson!).RootElement;
        Assert.Equal("Password", evidence.GetProperty("method").GetString());
        Assert.True(evidence.GetProperty("rememberMe").GetBoolean());
        Assert.Equal(AdminAudience, evidence.GetProperty("clientAudience").GetString());
        Assert.True(evidence.GetProperty("emailConfirmed").GetBoolean());
        AssertCarriesNoAddress(row);
    }

    [Fact]
    public async Task A_Refused_Admin_SignIn_For_An_Unknown_Address_Leaves_One_Admin_Failure_Row_Naming_Nobody_And_Never_The_Address()
    {
        await SeedAdminAsync();

        var response = await AdminClientAnonymous().PostAsJsonAsync("/api/AdminAuth/Login",
            new { email = UnknownEmail, password = Password, rememberMe = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await CustomerRowsAsync());
        var row = Assert.Single(await AdminRowsAsync());
        Assert.Equal("admin.session.login", row.Action);
        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.NotExistingUserWithEmail, row.ErrorCode);
        Assert.Equal("System", row.ActorId);
        Assert.Null(row.ActorEmail);
        Assert.Null(row.ResourceId);
        Assert.Null(row.BeforeJson);
        Assert.Null(row.AfterJson);
        Assert.Equal(HostTestTenants.Default, row.TenantId);
        AssertCarriesNoAddress(row);
    }

    [Fact]
    public async Task A_Refused_Admin_SignIn_With_A_Wrong_Password_On_A_Second_Operators_Administrator_Names_The_Account_And_Is_Stamped_With_That_Company()
    {
        await SeedAdminAsync(HostTestTenants.B);

        var response = await AdminClientAnonymous().PostAsJsonAsync("/api/AdminAuth/Login",
            new { email = AdminEmail, password = "Wrong-Password-999!", rememberMe = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await CustomerRowsAsync());
        var row = Assert.Single(await AdminRowsAsync());
        Assert.Equal("admin.session.login", row.Action);
        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.InvalidPassword, row.ErrorCode);
        Assert.Equal(AdminId, row.ActorId);
        Assert.Null(row.ActorEmail);
        Assert.Equal("User", row.ResourceType);
        Assert.Equal(AdminId, row.ResourceId);
        Assert.Null(row.AfterJson);
        Assert.Equal(HostTestTenants.B, row.TenantId);
        AssertCarriesNoAddress(row);
    }

    /// <summary>The handler, not the validator, refuses a non-administrator (a handler-returned auth failure is a 401); the validator had already named the account.</summary>
    [Fact]
    public async Task A_Customer_Refused_The_Admin_Host_Leaves_One_Admin_Failure_Row_Keyed_On_That_Account()
    {
        await SeedCustomerAsync();

        var response = await AdminClientAnonymous().PostAsJsonAsync("/api/AdminAuth/Login",
            new { email = CustomerEmail, password = Password, rememberMe = false });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await CustomerRowsAsync());
        var row = Assert.Single(await AdminRowsAsync());
        Assert.Equal("admin.session.login", row.Action);
        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.InsufficientPrivileges, row.ErrorCode);
        Assert.Equal(CustomerId, row.ActorId);
        Assert.Equal(CustomerId, row.ResourceId);
        Assert.Null(row.AfterJson);
        Assert.Equal(HostTestTenants.Default, row.TenantId);
        AssertCarriesNoAddress(row);
    }
}
