using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0064 D1 end to end over the real hosts with two operating companies: A (<c>cleansia-cz</c>,
/// the default market CZE) and B (<c>cleansia-sk</c>, SVK). Deactivating B delists SVK from every
/// market directory and from <c>Country/GetServiced</c>, refuses every anonymous and signed-in
/// booking-path write naming it with <c>country.not_serviced</c>, refuses B's cleaners on the partner
/// audiences and on refresh while its administrators and customers keep signing in; reactivation
/// restores all of it; the company holding the default market cannot be deactivated until the flag
/// moves; every act is refused under the right key and leaves its admin audit row.
/// </summary>
public sealed class CompanyLifecycleRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string GetRoute = "/api/AdminCompanyLifecycle/get";
    private const string DeactivateRoute = "/api/AdminCompanyLifecycle/deactivate";
    private const string ReactivateRoute = "/api/AdminCompanyLifecycle/reactivate";
    private const string Password = "12345678Test!";

    private const string EurId = "cur-eur-lifecycle";
    private const string SvkId = "country-svk-lifecycle";

    private const string AdminAId = "lifecycle-admin-a";
    private const string AdminAEmail = "lifecycle-admin-a@hosttests.local";
    private const string CustomerAId = "lifecycle-customer-a";
    private const string CustomerAEmail = "lifecycle-customer-a@hosttests.local";
    private const string AdminBId = "lifecycle-admin-b";
    private const string AdminBEmail = "lifecycle-admin-b@hosttests.local";
    private const string CleanerBId = "lifecycle-cleaner-b";
    private const string CleanerBEmail = "lifecycle-cleaner-b@hosttests.local";
    private const string GoogleCleanerBId = "lifecycle-google-cleaner-b";
    private const string GoogleCleanerBEmail = "lifecycle-google-cleaner-b@hosttests.local";
    private const string GoogleCleanerBToken = "lifecycle-google-cleaner-b-token";
    private const string CustomerBId = "lifecycle-customer-b";
    private const string CustomerBEmail = "lifecycle-customer-b@hosttests.local";

    private static readonly StubGoogleTokenVerifier Verifier = new(new Dictionary<string, GoogleVerifiedClaims>
    {
        [GoogleCleanerBToken] = new("sub-lifecycle-cleaner-b", GoogleCleanerBEmail, EmailVerified: true),
    });

    protected override void ConfigurePartnerHostServices(IServiceCollection services) => UseStubVerifier(services);

    protected override void ConfigureMobileHostServices(IServiceCollection services) => UseStubVerifier(services);

    private static void UseStubVerifier(IServiceCollection services)
    {
        services.RemoveAll<IGoogleTokenVerifier>();
        services.AddSingleton<IGoogleTokenVerifier>(Verifier);
    }

    private static string AdminToken(string userId, string email, string tenantId) =>
        TestJwtFactory.Mint(AdminAudience, userId, email, UserProfile.Administrator, tenantId: tenantId);

    private HttpClient AdminA() => AdminClient(AdminToken(AdminAId, AdminAEmail, HostTestTenants.A));

    private HttpClient AdminB() => AdminClient(AdminToken(AdminBId, AdminBEmail, HostTestTenants.B));

    private HttpClient CustomerB() =>
        CustomerClient(TestJwtFactory.Mint(CustomerAudience, CustomerBId, CustomerBEmail, UserProfile.Customer, tenantId: HostTestTenants.B));

    private static User Stamped(User user, string tenantId)
    {
        user.TenantId = tenantId;
        return user;
    }

    private async Task ArrangeTwoCompaniesAsync()
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);

            var eur = Currency.Create("EUR", "€", "Euro");
            eur.Id = EurId;
            eur.IsActive = true;
            eur.SetLoyaltyPointsDivisor(1m);
            ctx.Currencies.Add(eur);
            var slovakia = Country.Create("Slovakia", "SVK", "SK", isServiced: true);
            slovakia.Id = SvkId;
            ctx.Countries.Add(slovakia);
            ctx.CountryConfigurations.Add(
                CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m).AssignOperator(HostTestTenants.B));

            ctx.Users.AddRange(
                Stamped(DomainSeed.Admin(AdminAEmail), HostTestTenants.A).WithId(AdminAId),
                Stamped(DomainSeed.Customer(CustomerAEmail), HostTestTenants.A).WithId(CustomerAId),
                Stamped(DomainSeed.Admin(AdminBEmail), HostTestTenants.B).WithId(AdminBId),
                Stamped(DomainSeed.EmployeeUser(CleanerBEmail), HostTestTenants.B).WithId(CleanerBId),
                Stamped(User.CreateWithGoogle(GoogleCleanerBEmail, "Google", "Cleaner", "sub-lifecycle-cleaner-b").UpgradeToEmployee(), HostTestTenants.B).WithId(GoogleCleanerBId),
                Stamped(DomainSeed.Customer(CustomerBEmail), HostTestTenants.B).WithId(CustomerBId));
        });
    }

    private async Task DeactivateBAsync()
    {
        var response = await AdminB().PostAsync(DeactivateRoute, content: null);
        HttpAssert.IsOk(response);
    }

    private async Task<Tenant> TenantRowAsync(string tenantId) =>
        await QueryAsync(ctx => ctx.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId));

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static async Task<HashSet<string>> IsoCodesAsync(HttpResponseMessage response)
    {
        HttpAssert.IsOk(response);
        return (await BodyAsync(response)).EnumerateArray()
            .Select(m => m.GetProperty("isoCode").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task AssertMarketDirectoriesAsync(bool svkListed)
    {
        foreach (var response in new[]
                 {
                     await CustomerClientAnonymous().GetAsync("/api/Market/GetOverview"),
                     await PartnerClientAnonymous().GetAsync("/api/Market/GetOverview"),
                     await MobileClientAnonymous().GetAsync("/api/Market/GetOverview"),
                     await CustomerClientAnonymous().GetAsync("/api/Country/GetServiced"),
                 })
        {
            var codes = await IsoCodesAsync(response);
            Assert.Contains("CZ", codes);
            Assert.Equal(svkListed, codes.Contains("SVK"));
        }
    }

    private static async Task AssertRefusedWithAsync(HttpResponseMessage response, string expectedKey)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(expectedKey, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static object QuoteAt(string countryId) => new
    {
        selectedServiceIds = new[] { "service-lifecycle" },
        selectedPackageIds = Array.Empty<string>(),
        rooms = 2,
        bathrooms = 1,
        currencyId = (string?)null,
        countryId,
    };

    private static object GuestOrderAt(string countryId, string email) => new
    {
        customerName = "Guest Customer",
        customerEmail = email,
        customerPhone = "+421900000000",
        customerAddress = new { street = "Hlavna 1", city = "Bratislava", zipCode = "81101", countryId },
        savedAddressId = (string?)null,
        selectedPackageIds = Array.Empty<string>(),
        selectedServiceIds = new[] { "service-lifecycle" },
        rooms = 2,
        bathrooms = 1,
        extras = new Dictionary<string, bool>(),
        cleaningDate = DateTime.UtcNow.AddDays(5),
        paymentType = (int)PaymentType.Cash,
        currencyId = (string?)null,
        totalPrice = 100m,
        termsAccepted = true,
    };

    private static object Registration(string email, string countryId) => new
    {
        email,
        password = Password,
        firstName = "New",
        lastName = "Slovak",
        language = DomainSeed.LanguageCode,
        countryId,
        termsAccepted = true,
    };

    private static async Task<bool> IsRefusedNotServicedAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            return false;
        }

        return (await response.Content.ReadAsStringAsync()).Contains(BusinessErrorMessage.CountryNotServiced, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonAdmin_callers_are_403d_and_anonymous_401d_on_every_lifecycle_route()
    {
        await ArrangeTwoCompaniesAsync();
        var cleaner = AdminClient(TestJwtFactory.Mint(AdminAudience, CleanerBId, CleanerBEmail, UserProfile.Employee, tenantId: HostTestTenants.B));
        var customer = AdminClient(TestJwtFactory.Mint(AdminAudience, CustomerBId, CustomerBEmail, UserProfile.Customer, tenantId: HostTestTenants.B));

        HttpAssert.IsForbidden(await cleaner.GetAsync(GetRoute));
        HttpAssert.IsForbidden(await cleaner.PostAsync(DeactivateRoute, content: null));
        HttpAssert.IsForbidden(await customer.PostAsync(ReactivateRoute, content: null));
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().GetAsync(GetRoute));
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().PostAsync(DeactivateRoute, content: null));
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().PostAsync(ReactivateRoute, content: null));
        Assert.True((await TenantRowAsync(HostTestTenants.B)).IsActive);
    }

    /// <summary>TC-LC-DEACT-1 and TC-LC-DEACT-5 on the directories.</summary>
    [Fact]
    public async Task A_deactivated_companys_market_leaves_every_directory_and_returns_on_reactivation()
    {
        await ArrangeTwoCompaniesAsync();
        await AssertMarketDirectoriesAsync(svkListed: true);

        await DeactivateBAsync();
        await AssertMarketDirectoriesAsync(svkListed: false);

        HttpAssert.IsOk(await AdminB().PostAsync(ReactivateRoute, content: null));
        await AssertMarketDirectoriesAsync(svkListed: true);
    }

    /// <summary>TC-LC-DEACT-2: the anonymous writes that name the closed market.</summary>
    [Fact]
    public async Task Anonymous_register_quote_and_guest_order_naming_the_closed_market_are_refused_and_write_nothing()
    {
        await ArrangeTwoCompaniesAsync();
        var anonymous = CustomerClientAnonymous();

        Assert.False(await IsRefusedNotServicedAsync(await anonymous.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId))));
        Assert.False(await IsRefusedNotServicedAsync(await anonymous.PostAsJsonAsync("/api/Order/CreateOrder", GuestOrderAt(SvkId, "guest-open@hosttests.local"))));

        await DeactivateBAsync();

        var register = await anonymous.PostAsJsonAsync("/api/Auth/Register", Registration("new-slovak@hosttests.local", SvkId));
        await HttpAssert.AssertBusinessErrorAsync(register, BusinessErrorMessage.CountryNotServiced);
        Assert.Equal(0, await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "new-slovak@hosttests.local")));

        var quote = await anonymous.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId));
        await HttpAssert.AssertBusinessErrorAsync(quote, BusinessErrorMessage.CountryNotServiced);

        var order = await anonymous.PostAsJsonAsync("/api/Order/CreateOrder", GuestOrderAt(SvkId, "guest-closed@hosttests.local"));
        await HttpAssert.AssertBusinessErrorAsync(order, BusinessErrorMessage.CountryNotServiced);
        Assert.Equal(0, await QueryAsync(ctx => ctx.Orders.IgnoreQueryFilters().CountAsync()));

        HttpAssert.IsOk(await AdminB().PostAsync(ReactivateRoute, content: null));
        var registerAgain = await anonymous.PostAsJsonAsync("/api/Auth/Register", Registration("new-slovak@hosttests.local", SvkId));
        HttpAssert.IsOk(registerAgain);
        var landed = await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "new-slovak@hosttests.local"));
        Assert.Equal(HostTestTenants.B, landed.TenantId);
    }

    /// <summary>
    /// TC-LC-DEACT-3: the signed-in customer's booking-path writes at the closed market. The quote and
    /// the booking read the same repository predicate, so the quote stands for the booking here — a
    /// bookable order body needs the whole catalogue seeded, and the guest order above already proves
    /// the write path.
    /// </summary>
    [Fact]
    public async Task A_signed_in_customers_quote_and_plus_purchase_at_the_closed_market_are_refused_and_the_catalogue_is_empty()
    {
        await ArrangeTwoCompaniesAsync();
        var customer = CustomerB();
        var subscribe = new { PlanCode = "PLUS_MONTHLY", PaymentMethodConfirmed = true, CountryId = SvkId, IdempotencyToken = "lifecycle-sub-1" };

        Assert.False(await IsRefusedNotServicedAsync(await customer.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId))));
        Assert.False(await IsRefusedNotServicedAsync(await customer.PostAsJsonAsync("/api/Membership/Subscribe", subscribe)));
        HttpAssert.IsOk(await customer.GetAsync($"/api/Service/GetOverview?countryId={SvkId}"));

        await DeactivateBAsync();

        await HttpAssert.AssertBusinessErrorAsync(await customer.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId)), BusinessErrorMessage.CountryNotServiced);
        await HttpAssert.AssertBusinessErrorAsync(await customer.PostAsJsonAsync("/api/Membership/Subscribe", subscribe), BusinessErrorMessage.CountryNotServiced);
        Assert.Equal(0, await QueryAsync(ctx => ctx.UserMemberships.IgnoreQueryFilters().CountAsync()));

        var overview = await customer.GetAsync($"/api/Service/GetOverview?countryId={SvkId}");
        HttpAssert.IsOk(overview);
        Assert.Empty((await BodyAsync(overview)).EnumerateArray());
    }

    /// <summary>TC-LC-DEACT-4 and the sign-in half of TC-LC-DEACT-5.</summary>
    [Fact]
    public async Task A_deactivated_companys_cleaners_are_refused_on_the_partner_audiences_while_everyone_else_signs_in()
    {
        const string plantedRefresh = "lifecycle-planted-partner-refresh";
        await ArrangeTwoCompaniesAsync();
        await SeedAsync(ctx =>
        {
            var token = RefreshTokenEntity.Create(
                userId: CleanerBId,
                tokenHash: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plantedRefresh))).ToLowerInvariant(),
                expiresAt: DateTimeOffset.UtcNow.AddDays(7),
                audience: PartnerAudience,
                deviceLabel: null,
                ipAddress: null);
            token.TenantId = HostTestTenants.B;
            ctx.RefreshTokens.Add(token);
            return Task.CompletedTask;
        });
        var cleanerLogin = new { email = CleanerBEmail, password = Password, rememberMe = true };

        await DeactivateBAsync();

        await AssertRefusedWithAsync(await PartnerClientAnonymous().PostAsJsonAsync("/api/Auth/Login", cleanerLogin), BusinessErrorMessage.CompanyDeactivated);
        await AssertRefusedWithAsync(await MobileClientAnonymous().PostAsJsonAsync("/api/Auth/Login", cleanerLogin), BusinessErrorMessage.CompanyDeactivated);
        await AssertRefusedWithAsync(
            await PartnerClientAnonymous().PostAsJsonAsync("/api/Auth/GoogleAuth", new { token = GoogleCleanerBToken, googleId = "ignored", email = GoogleCleanerBEmail, firstName = "G", lastName = "C", termsAccepted = true }),
            BusinessErrorMessage.CompanyDeactivated);
        var refresh = await PartnerClientAnonymous().PostAsJsonAsync("/api/Auth/RefreshToken", new { token = plantedRefresh });
        await AssertRefusedWithAsync(refresh, BusinessErrorMessage.CompanyDeactivated);

        var cleanerTokens = await QueryAsync(ctx => ctx.RefreshTokens.IgnoreQueryFilters().Where(t => t.UserId == CleanerBId || t.UserId == GoogleCleanerBId).ToListAsync());
        var planted = Assert.Single(cleanerTokens);
        Assert.Null(planted.RevokedReason);

        // The customer host is the surviving GDPR channel for the company's cleaners.
        var customerHostLogin = await CustomerClientAnonymous().PostAsJsonAsync("/api/Auth/Login", cleanerLogin);
        HttpAssert.IsOk(customerHostLogin);
        Assert.Contains(customerHostLogin.Headers.GetValues("Set-Cookie"), c => c.StartsWith("customer_token=", StringComparison.Ordinal));
        var cleanerOnCustomerHost = CustomerClient(TestJwtFactory.Mint(CustomerAudience, CleanerBId, CleanerBEmail, UserProfile.Employee, tenantId: HostTestTenants.B));
        HttpAssert.IsOk(await cleanerOnCustomerHost.PostAsync("/api/v1/Gdpr/export", content: null));
        HttpAssert.IsOk(await cleanerOnCustomerHost.PostAsync("/api/v1/Gdpr/delete-account", content: null));

        var adminBLogin = new { email = AdminBEmail, password = Password, rememberMe = true };
        HttpAssert.IsOk(await AdminClientAnonymous().PostAsJsonAsync("/api/AdminAuth/Login", adminBLogin));
        HttpAssert.IsOk(await PartnerClientAnonymous().PostAsJsonAsync("/api/Auth/Login", adminBLogin));
        HttpAssert.IsOk(await CustomerClientAnonymous().PostAsJsonAsync("/api/Auth/Login", new { email = CustomerBEmail, password = Password, rememberMe = true }));

        var adminALogin = new { email = AdminAEmail, password = Password, rememberMe = true };
        HttpAssert.IsOk(await AdminClientAnonymous().PostAsJsonAsync("/api/AdminAuth/Login", adminALogin));
        HttpAssert.IsOk(await PartnerClientAnonymous().PostAsJsonAsync("/api/Auth/Login", adminALogin));
        HttpAssert.IsOk(await MobileClientAnonymous().PostAsJsonAsync("/api/Auth/Login", adminALogin));
        HttpAssert.IsOk(await CustomerClientAnonymous().PostAsJsonAsync("/api/Auth/Login", new { email = CustomerAEmail, password = Password, rememberMe = true }));

        HttpAssert.IsOk(await AdminB().PostAsync(ReactivateRoute, content: null));
        var googleCleanerLogin = await MobileClientAnonymous().PostAsJsonAsync(
            "/api/Auth/GoogleAuth", new { token = GoogleCleanerBToken, googleId = "ignored", email = GoogleCleanerBEmail, firstName = "G", lastName = "C", termsAccepted = true });
        HttpAssert.IsOk(googleCleanerLogin);
        Assert.Equal("Employee", (await BodyAsync(googleCleanerLogin)).GetProperty("role").GetString());
        var reopened = await TenantRowAsync(HostTestTenants.B);
        Assert.True(reopened.IsActive);
        Assert.Null(reopened.DeactivatedOn);
        Assert.Null(reopened.DeactivatedBy);
        Assert.Null(reopened.WindDownFrom);
    }

    /// <summary>TC-LC-DEACT-6: the platform-protecting refusal, and the readiness gates that read the operator.</summary>
    [Fact]
    public async Task The_company_holding_the_default_market_cannot_be_deactivated_until_the_flag_moves()
    {
        await ArrangeTwoCompaniesAsync();

        var refused = await AdminA().PostAsync(DeactivateRoute, content: null);
        await HttpAssert.AssertBusinessErrorAsync(refused, BusinessErrorMessage.CompanyOperatesDefaultMarket);
        Assert.True((await TenantRowAsync(HostTestTenants.A)).IsActive);
        var dto = await BodyAsync(await AdminA().GetAsync(GetRoute));
        Assert.True(dto.GetProperty("operatesDefaultMarket").GetBoolean());

        HttpAssert.IsOk(await AdminA().PutAsJsonAsync($"/api/AdminCountry/{SvkId}/default-market", new { }));

        HttpAssert.IsOk(await AdminA().PostAsync(DeactivateRoute, content: null));
        Assert.False((await TenantRowAsync(HostTestTenants.A)).IsActive);
    }

    [Fact]
    public async Task A_deactivated_operators_country_can_be_neither_the_default_market_nor_switched_on()
    {
        await ArrangeTwoCompaniesAsync();
        await DeactivateBAsync();

        var flag = await AdminA().PutAsJsonAsync($"/api/AdminCountry/{SvkId}/default-market", new { });
        await HttpAssert.AssertBusinessErrorAsync(flag, BusinessErrorMessage.CountryNotServiced);

        HttpAssert.IsOk(await AdminA().PutAsJsonAsync($"/api/AdminCountry/{SvkId}/serviced", new { IsServiced = false }));
        var switchOn = await AdminA().PutAsJsonAsync($"/api/AdminCountry/{SvkId}/serviced", new { IsServiced = true });
        await HttpAssert.AssertBusinessErrorAsync(switchOn, BusinessErrorMessage.CountryMarketNotReady);
        Assert.False((await QueryAsync(ctx => ctx.Countries.IgnoreQueryFilters().SingleAsync(c => c.Id == SvkId))).IsServiced);
    }

    [Fact]
    public async Task Deactivation_stamps_the_row_leaves_one_audit_row_and_is_refused_twice_and_on_a_frozen_company()
    {
        await ArrangeTwoCompaniesAsync();

        var first = await AdminB().PostAsync(DeactivateRoute, content: null);
        HttpAssert.IsOk(first);
        Assert.Equal((int)CompanyLifecycleState.Deactivated, (await BodyAsync(first)).GetProperty("state").GetInt32());

        var row = await TenantRowAsync(HostTestTenants.B);
        Assert.False(row.IsActive);
        Assert.Equal(AdminBId, row.DeactivatedBy);
        Assert.NotNull(row.DeactivatedOn);

        var audit = Assert.Single(await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().ToListAsync()));
        Assert.Equal("company.deactivate", audit.Action);
        Assert.True(audit.Success);
        Assert.Equal(AdminBId, audit.ActorId);
        Assert.Equal("Tenant", audit.ResourceType);
        Assert.Equal(HostTestTenants.B, audit.ResourceId);
        Assert.Equal(HostTestTenants.B, audit.TenantId);
        Assert.Equal("operating", JsonDocument.Parse(audit.BeforeJson!).RootElement.GetProperty("state").GetString());
        Assert.Equal("deactivated", JsonDocument.Parse(audit.AfterJson!).RootElement.GetProperty("state").GetString());

        var second = await AdminB().PostAsync(DeactivateRoute, content: null);
        await HttpAssert.AssertBusinessErrorAsync(second, BusinessErrorMessage.CompanyAlreadyDeactivated);

        await SeedAsync(async ctx =>
        {
            var tenant = await ctx.Tenants.SingleAsync(t => t.Id == HostTestTenants.B);
            tenant.RequestWindDown(new DateOnly(2026, 10, 1), AdminBId, DateTimeOffset.UtcNow);
            tenant.RequestArchive(AdminBId, DateTimeOffset.UtcNow);
        });

        await HttpAssert.AssertBusinessErrorAsync(await AdminB().PostAsync(DeactivateRoute, content: null), BusinessErrorMessage.CompanyArchived);
        await HttpAssert.AssertBusinessErrorAsync(await AdminB().PostAsync(ReactivateRoute, content: null), BusinessErrorMessage.CompanyArchived);
        await HttpAssert.AssertBusinessErrorAsync(await AdminA().PostAsync(ReactivateRoute, content: null), BusinessErrorMessage.CompanyNotDeactivated);
        Assert.Equal((int)CompanyLifecycleState.Frozen, (await BodyAsync(await AdminB().GetAsync(GetRoute))).GetProperty("state").GetInt32());
    }

    [Fact]
    public async Task The_lifecycle_read_names_the_company_its_state_and_its_facts_and_carries_no_tenant_id()
    {
        await ArrangeTwoCompaniesAsync();

        var response = await AdminA().GetAsync(GetRoute);

        HttpAssert.IsOk(response);
        var dto = await BodyAsync(response);
        Assert.Equal("Cleansia CZ s.r.o.", dto.GetProperty("name").GetString());
        Assert.Equal((int)CompanyLifecycleState.Operating, dto.GetProperty("state").GetInt32());
        Assert.True(dto.GetProperty("operatesDefaultMarket").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("deactivatedOn").ValueKind);
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("chargebackHorizonEndsOn").ValueKind);
        foreach (var fact in new[] { "openOrders", "openOrdersOnOrAfterWindDownFrom", "activeTemplates", "activeMemberships", "creditBalances", "pendingRefunds", "ordersAwaitingPay", "ordersAwaitingReceipt", "receiptsAwaitingFiscalRegistration", "openPayPeriods", "unpaidInvoices", "uninvoicedPayRows", "openDisputes" })
        {
            Assert.Equal(0, dto.GetProperty(fact).GetInt32());
        }

        Assert.DoesNotContain(dto.EnumerateObject(), p => p.Name.Contains("tenant", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("blob", StringComparison.OrdinalIgnoreCase));

        var seenByB = await BodyAsync(await AdminB().GetAsync(GetRoute));
        Assert.Equal("Cleansia SK s.r.o.", seenByB.GetProperty("name").GetString());
        Assert.False(seenByB.GetProperty("operatesDefaultMarket").GetBoolean());
    }

    private sealed class StubGoogleTokenVerifier(IReadOnlyDictionary<string, GoogleVerifiedClaims> claimsByToken) : IGoogleTokenVerifier
    {
        public Task<GoogleVerifiedClaims?> VerifyAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(claimsByToken.GetValueOrDefault(token));
    }
}

file static class UserSeedExtensions
{
    public static User WithId(this User user, string id)
    {
        user.Id = id;
        return user;
    }
}
