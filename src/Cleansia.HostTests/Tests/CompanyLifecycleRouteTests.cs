using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0064 D1 end to end over the real hosts with two operating companies: A (<c>cleansia-cz</c>,
/// the default market CZE) and B (<c>cleansia-sk</c>, SVK, with one service priced and paid in EUR, a
/// Plus plan priced in EUR and a customer holding a saved Bratislava address). Deactivating B delists
/// SVK from every market directory and from <c>Country/GetServiced</c>, refuses every anonymous and
/// signed-in booking-path write naming it with <c>country.not_serviced</c>, refuses B's cleaners on the
/// partner audiences and on refresh while its administrators and customers keep signing in;
/// reactivation restores all of it; the company holding the default market cannot be deactivated
/// until the flag moves; every act is refused under the right key and leaves its admin audit row.
///
/// <para>Every refusal is paired with the same request succeeding while B operates — a quote priced
/// in EUR, a Plus purchase through a recording Stripe client, a recurring booking, a saved address, a
/// non-empty catalogue — so a predicate that refused everything, or one that never delisted the
/// catalogue, fails here. The one write that does not succeed on this harness is the booking itself
/// (no serviced city is seeded, and past that <c>CreateOrder</c> geocodes an inline address through
/// the outbound Mapbox seam); while B operates it is asserted on <c>city.not_serviced</c>, the refusal
/// that is reachable only once the address resolver has admitted the country.</para>
/// </summary>
public sealed class CompanyLifecycleRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string GetRoute = "/api/AdminCompanyLifecycle/get";
    private const string DeactivateRoute = "/api/AdminCompanyLifecycle/deactivate";
    private const string ReactivateRoute = "/api/AdminCompanyLifecycle/reactivate";
    private const string WindDownRoute = "/api/AdminCompanyLifecycle/wind-down";
    private const string Password = "12345678Test!";

    private const string EurId = "cur-eur-lifecycle";
    private const string SvkId = "country-svk-lifecycle";
    private const string ServiceId = "service-lifecycle";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string SavedAddressBId = "lifecycle-saved-address-b";

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

    private readonly RecordingStripeClient _stripe = new();

    protected override void ConfigureCustomerHostServices(IServiceCollection services)
    {
        services.RemoveAll<IStripeClient>();
        services.AddSingleton<IStripeClient>(_stripe);
    }

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

            var category = ServiceCategory.Create("lifecycle", "Lifecycle", "Category under test");
            ctx.Add(category);
            var service = Service.Create(category.Id, "Lifecycle clean", "Under test", 60);
            service.Id = ServiceId;
            ctx.Add(service);
            ctx.ServicePrices.Add(ServicePrice.Create(ServiceId, EurId, 40m, 10m));
            var pay = EmployeePayConfig.CreateForService(ServiceId, 10m, EurId);
            pay.TenantId = HostTestTenants.B;
            ctx.Add(pay);

            var plan = DomainSeed.MembershipPlan(PlanCode);
            ctx.MembershipPlans.Add(plan);
            ctx.MembershipPlanPrices.Add(MembershipPlanPrice.Create(plan.Id, EurId, 7.99m, $"price_hosttest_{PlanCode}_lifecycle"));

            ctx.Users.AddRange(
                Stamped(DomainSeed.Admin(AdminAEmail), HostTestTenants.A).WithId(AdminAId),
                Stamped(DomainSeed.Customer(CustomerAEmail), HostTestTenants.A).WithId(CustomerAId),
                Stamped(DomainSeed.Admin(AdminBEmail), HostTestTenants.B).WithId(AdminBId),
                Stamped(DomainSeed.EmployeeUser(CleanerBEmail), HostTestTenants.B).WithId(CleanerBId),
                Stamped(User.CreateWithGoogle(GoogleCleanerBEmail, "Google", "Cleaner", "sub-lifecycle-cleaner-b").UpgradeToEmployee(), HostTestTenants.B).WithId(GoogleCleanerBId),
                Stamped(DomainSeed.Customer(CustomerBEmail), HostTestTenants.B).WithId(CustomerBId));

            var bratislava = Address.Create("Hlavna 1", "Bratislava", "81101", SvkId, null, 48.1486, 17.1077);
            bratislava.TenantId = HostTestTenants.B;
            ctx.Addresses.Add(bratislava);
            var savedInSlovakia = SavedAddress.Create(CustomerBId, bratislava.Id, "Home", isDefault: true);
            savedInSlovakia.Id = SavedAddressBId;
            savedInSlovakia.TenantId = HostTestTenants.B;
            ctx.SavedAddresses.Add(savedInSlovakia);
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

    private static readonly DateTime CleaningDate = DateTime.UtcNow.Date.AddDays(5).AddHours(9);

    private static object QuoteAt(string countryId) => new
    {
        selectedServiceIds = new[] { ServiceId },
        selectedPackageIds = Array.Empty<string>(),
        rooms = 2,
        bathrooms = 1,
        currencyId = (string?)null,
        cleaningDate = CleaningDate,
        countryId,
    };

    private static object OrderAt(string countryId, string email, decimal totalPrice) => new
    {
        customerName = "Slovak Customer",
        customerEmail = email,
        customerPhone = "+421900000000",
        customerAddress = new { street = "Hlavna 1", city = "Bratislava", zipCode = "81101", countryId },
        savedAddressId = (string?)null,
        selectedPackageIds = Array.Empty<string>(),
        selectedServiceIds = new[] { ServiceId },
        rooms = 2,
        bathrooms = 1,
        extras = new Dictionary<string, bool>(),
        cleaningDate = CleaningDate,
        paymentType = (int)PaymentType.Cash,
        currencyId = (string?)null,
        totalPrice,
        termsAccepted = true,
    };

    private static object RecurringBookingAt(string savedAddressId) => new
    {
        frequency = (int)RecurrenceFrequency.Weekly,
        dayOfWeek = (int)System.DayOfWeek.Tuesday,
        timeOfDay = "09:00",
        rooms = 2,
        bathrooms = 1,
        savedAddressId,
        selectedServiceIds = new[] { ServiceId },
        selectedPackageIds = Array.Empty<string>(),
        paymentType = (int)PaymentType.Card,
        startsOn = DateTime.UtcNow.Date.AddDays(3),
        endsOn = (DateTime?)null,
        preferredEmployeeId = (string?)null,
    };

    private static object SavedAddressAt(string countryId, string label) => new
    {
        label,
        street = "Obchodna 2",
        city = "Bratislava",
        zipCode = "81106",
        countryId,
        setAsDefault = false,
        latitude = 48.1461,
        longitude = 17.1116,
    };

    /// <summary>The EUR total the calculator quotes for <see cref="QuoteAt"/> — what the booking must resubmit.</summary>
    private static async Task<decimal> QuotedEurTotalAsync(HttpResponseMessage quote)
    {
        HttpAssert.IsOk(quote);
        var body = await BodyAsync(quote);
        Assert.Equal("EUR", body.GetProperty("currencyCode").GetString());
        var total = body.GetProperty("totalPrice").GetDecimal();
        Assert.True(total > 0m);
        return total;
    }

    private async Task<HashSet<string>> ServiceOverviewAsync(HttpClient client)
    {
        var overview = await client.GetAsync($"/api/Service/GetOverview?countryId={SvkId}");
        HttpAssert.IsOk(overview);
        return (await BodyAsync(overview)).EnumerateArray()
            .Select(s => s.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

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

    /// <summary>
    /// TC-LC-DEACT-2: the anonymous writes that name the closed market. While B operates the quote is
    /// priced in EUR and the guest order carrying that price gets past the country predicate (refused
    /// on the city, which no test seeds); once B is deactivated the same bytes are refused on the
    /// country and nothing is written; reactivation admits them again.
    /// </summary>
    [Fact]
    public async Task Anonymous_register_quote_and_guest_order_naming_the_closed_market_are_refused_and_write_nothing()
    {
        await ArrangeTwoCompaniesAsync();
        var anonymous = CustomerClientAnonymous();

        var quotedTotal = await QuotedEurTotalAsync(await anonymous.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId)));
        var openOrder = await anonymous.PostAsJsonAsync("/api/Order/CreateOrder", OrderAt(SvkId, "guest-open@hosttests.local", quotedTotal));
        await HttpAssert.AssertBusinessErrorAsync(openOrder, BusinessErrorMessage.CityNotServiced);

        await DeactivateBAsync();

        var register = await anonymous.PostAsJsonAsync("/api/Auth/Register", Registration("new-slovak@hosttests.local", SvkId));
        await HttpAssert.AssertBusinessErrorAsync(register, BusinessErrorMessage.CountryNotServiced);
        Assert.Equal(0, await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "new-slovak@hosttests.local")));

        var quote = await anonymous.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId));
        await HttpAssert.AssertBusinessErrorAsync(quote, BusinessErrorMessage.CountryNotServiced);

        var order = await anonymous.PostAsJsonAsync("/api/Order/CreateOrder", OrderAt(SvkId, "guest-closed@hosttests.local", quotedTotal));
        await HttpAssert.AssertBusinessErrorAsync(order, BusinessErrorMessage.CountryNotServiced);
        Assert.Equal(0, await QueryAsync(ctx => ctx.Orders.IgnoreQueryFilters().CountAsync()));

        HttpAssert.IsOk(await AdminB().PostAsync(ReactivateRoute, content: null));
        Assert.Equal(quotedTotal, await QuotedEurTotalAsync(await anonymous.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId))));
        var registerAgain = await anonymous.PostAsJsonAsync("/api/Auth/Register", Registration("new-slovak@hosttests.local", SvkId));
        HttpAssert.IsOk(registerAgain);
        var landed = await QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "new-slovak@hosttests.local"));
        Assert.Equal(HostTestTenants.B, landed.TenantId);
    }

    /// <summary>
    /// TC-LC-DEACT-3 and its half of TC-LC-DEACT-5: B's customer quotes in EUR, buys Plus through the
    /// recording Stripe client, schedules a recurring booking at the saved Bratislava address, saves a
    /// second Slovak address and browses a non-empty catalogue while B operates; once B is deactivated
    /// every one of those is refused <c>country.not_serviced</c> with no row written, the booking is
    /// refused by <c>CreateOrder</c>'s own validator before the country is ever named (its price chain
    /// runs ahead of the address resolver — pre-existing, and <c>CreateOrder</c> is diff-empty by the
    /// ADR), and the catalogue answers empty; reactivation restores the quote and the catalogue.
    /// </summary>
    [Fact]
    public async Task A_signed_in_customers_booking_path_writes_at_the_closed_market_are_refused_and_the_catalogue_is_empty()
    {
        await ArrangeTwoCompaniesAsync();
        var customer = CustomerB();
        var subscribe = new { PlanCode, PaymentMethodConfirmed = true, CountryId = SvkId, IdempotencyToken = "lifecycle-sub-1" };

        var quotedTotal = await QuotedEurTotalAsync(await customer.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId)));
        Assert.Contains(ServiceId, await ServiceOverviewAsync(customer));
        HttpAssert.IsOk(await customer.PostAsJsonAsync("/api/Membership/Subscribe", subscribe));
        Assert.Equal(1, await QueryAsync(ctx => ctx.UserMemberships.IgnoreQueryFilters().CountAsync(m => m.UserId == CustomerBId)));
        HttpAssert.IsOk(await customer.PostAsJsonAsync("/api/RecurringBooking/Create", RecurringBookingAt(SavedAddressBId)));
        Assert.Equal(1, await QueryAsync(ctx => ctx.RecurringBookingTemplates.IgnoreQueryFilters().CountAsync(t => t.UserId == CustomerBId)));
        HttpAssert.IsOk(await customer.PostAsJsonAsync("/api/SavedAddress/Add", SavedAddressAt(SvkId, "Office")));
        Assert.Equal(2, await QueryAsync(ctx => ctx.SavedAddresses.IgnoreQueryFilters().CountAsync(a => a.UserId == CustomerBId)));
        var openOrder = await customer.PostAsJsonAsync("/api/Order/CreateOrder", OrderAt(SvkId, CustomerBEmail, quotedTotal));
        await HttpAssert.AssertBusinessErrorAsync(openOrder, BusinessErrorMessage.CityNotServiced);

        await DeactivateBAsync();

        await HttpAssert.AssertBusinessErrorAsync(await customer.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId)), BusinessErrorMessage.CountryNotServiced);
        // A signed-in booking into a country nobody services is stopped by CreateOrder's validator
        // before the address resolver can name the country: the unserviced country resolves to no
        // market, the price chain judges the selection in the platform default currency and refuses
        // it there. The claim here is that the booking no longer reaches the city check it reached
        // while B operated, and writes nothing.
        var closedOrder = await customer.PostAsJsonAsync("/api/Order/CreateOrder", OrderAt(SvkId, CustomerBEmail, quotedTotal));
        Assert.Equal(HttpStatusCode.BadRequest, closedOrder.StatusCode);
        Assert.DoesNotContain(BusinessErrorMessage.CityNotServiced, await closedOrder.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, await QueryAsync(ctx => ctx.Orders.IgnoreQueryFilters().CountAsync()));
        await HttpAssert.AssertBusinessErrorAsync(await customer.PostAsJsonAsync("/api/Membership/Subscribe", subscribe with { IdempotencyToken = "lifecycle-sub-2" }), BusinessErrorMessage.CountryNotServiced);
        Assert.Equal(1, await QueryAsync(ctx => ctx.UserMemberships.IgnoreQueryFilters().CountAsync(m => m.UserId == CustomerBId)));
        await HttpAssert.AssertBusinessErrorAsync(await customer.PostAsJsonAsync("/api/RecurringBooking/Create", RecurringBookingAt(SavedAddressBId)), BusinessErrorMessage.CountryNotServiced);
        Assert.Equal(1, await QueryAsync(ctx => ctx.RecurringBookingTemplates.IgnoreQueryFilters().CountAsync(t => t.UserId == CustomerBId)));
        await HttpAssert.AssertBusinessErrorAsync(await customer.PostAsJsonAsync("/api/SavedAddress/Add", SavedAddressAt(SvkId, "Parents")), BusinessErrorMessage.CountryNotServiced);
        Assert.Equal(2, await QueryAsync(ctx => ctx.SavedAddresses.IgnoreQueryFilters().CountAsync(a => a.UserId == CustomerBId)));
        Assert.Empty(await ServiceOverviewAsync(customer));

        HttpAssert.IsOk(await AdminB().PostAsync(ReactivateRoute, content: null));
        Assert.Equal(quotedTotal, await QuotedEurTotalAsync(await customer.PostAsJsonAsync("/api/Order/Quote", QuoteAt(SvkId))));
        Assert.Contains(ServiceId, await ServiceOverviewAsync(customer));
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

    private static object WindDownFrom(DateOnly? fromDate) => new { fromDate = fromDate?.ToString("yyyy-MM-dd") };

    /// <summary>The sweep's message key is the request instant to the second: two acts within one second ask for one run.</summary>
    private static async Task WaitForTheNextSecondAsync()
    {
        var second = DateTime.UtcNow.Second;
        while (DateTime.UtcNow.Second == second)
        {
            await Task.Delay(50);
        }
    }

    private async Task<List<Cleansia.Core.Domain.Outbox.OutboxMessage>> WindDownMessagesAsync() =>
        await QueryAsync(ctx => ctx.OutboxMessages.IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.CompanyWindDown)
            .OrderBy(m => m.CreatedOn)
            .ToListAsync());

    /// <summary>
    /// ADR-0064 D2 and TC-LC-WD-5 on the route: the date is set once and never in the past, the request
    /// stamps the row, records one sweep message under B and one audit row, a re-run needs no date and
    /// is refused only while a run is fresh.
    /// </summary>
    [Fact]
    public async Task A_wind_down_date_is_set_once_starts_the_sweep_and_a_re_run_waits_for_a_fresh_run_only()
    {
        await ArrangeTwoCompaniesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(30);

        await HttpAssert.AssertBusinessErrorAsync(
            await AdminB().PostAsJsonAsync(WindDownRoute, WindDownFrom(today.AddDays(-2))), BusinessErrorMessage.CompanyWindDownDateInPast);
        await HttpAssert.AssertBusinessErrorAsync(
            await AdminB().PostAsJsonAsync(WindDownRoute, WindDownFrom(null)), BusinessErrorMessage.Required);
        Assert.Empty(await WindDownMessagesAsync());
        Assert.Null((await TenantRowAsync(HostTestTenants.B)).WindDownFrom);

        var first = await AdminB().PostAsJsonAsync(WindDownRoute, WindDownFrom(from));
        HttpAssert.IsOk(first);
        var body = await BodyAsync(first);
        Assert.Equal((int)CompanyLifecycleState.WindingDown, body.GetProperty("state").GetInt32());
        Assert.Equal(from.ToString("yyyy-MM-dd"), body.GetProperty("windDownFrom").GetString());

        var row = await TenantRowAsync(HostTestTenants.B);
        Assert.Equal(from, row.WindDownFrom);
        Assert.Equal(AdminBId, row.WindDownRequestedBy);
        Assert.NotNull(row.WindDownRequestedOn);

        var message = Assert.Single(await WindDownMessagesAsync());
        Assert.Equal(HostTestTenants.B, message.TenantId);
        Assert.StartsWith($"wind-down:{HostTestTenants.B}:", message.MessageKey, StringComparison.Ordinal);

        var audits = await QueryAsync(ctx => ctx.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.All(audits, a => Assert.Equal("company.wind_down", a.Action));
        var audit = Assert.Single(audits, a => a.Success);
        Assert.Equal(AdminBId, audit.ActorId);
        Assert.Equal("Tenant", audit.ResourceType);
        Assert.Equal(HostTestTenants.B, audit.ResourceId);
        Assert.Equal("operating", JsonDocument.Parse(audit.BeforeJson!).RootElement.GetProperty("state").GetString());
        Assert.Equal("windingDown", JsonDocument.Parse(audit.AfterJson!).RootElement.GetProperty("state").GetString());
        Assert.Equal(from.ToString("yyyy-MM-dd"), JsonDocument.Parse(audit.AfterJson!).RootElement.GetProperty("windDownFrom").GetString());

        await HttpAssert.AssertBusinessErrorAsync(
            await AdminB().PostAsJsonAsync(WindDownRoute, WindDownFrom(from.AddDays(1))), BusinessErrorMessage.CompanyWindDownAlreadyRequested);
        Assert.Equal(from, (await TenantRowAsync(HostTestTenants.B)).WindDownFrom);

        await WaitForTheNextSecondAsync();
        HttpAssert.IsOk(await AdminB().PostAsJsonAsync(WindDownRoute, WindDownFrom(null)));
        Assert.Equal(2, (await WindDownMessagesAsync()).Count);

        await SeedAsync(async ctx =>
        {
            var tenant = await ctx.Tenants.SingleAsync(t => t.Id == HostTestTenants.B);
            tenant.StartWindDownRun(DateTimeOffset.UtcNow.AddMinutes(-10));
        });
        await HttpAssert.AssertBusinessErrorAsync(
            await AdminB().PostAsJsonAsync(WindDownRoute, WindDownFrom(null)), BusinessErrorMessage.CompanyWindDownInProgress);
        Assert.Equal(2, (await WindDownMessagesAsync()).Count);

        await SeedAsync(async ctx =>
        {
            var tenant = await ctx.Tenants.SingleAsync(t => t.Id == HostTestTenants.B);
            tenant.StartWindDownRun(DateTimeOffset.UtcNow.AddHours(-2));
        });
        await WaitForTheNextSecondAsync();
        HttpAssert.IsOk(await AdminB().PostAsJsonAsync(WindDownRoute, WindDownFrom(null)));
        Assert.Equal(3, (await WindDownMessagesAsync()).Count);

        HttpAssert.IsForbidden(await AdminClient(TestJwtFactory.Mint(AdminAudience, CleanerBId, CleanerBEmail, UserProfile.Employee, tenantId: HostTestTenants.B)).PostAsJsonAsync(WindDownRoute, WindDownFrom(from)));
        HttpAssert.IsUnauthorized(await AdminClientAnonymous().PostAsJsonAsync(WindDownRoute, WindDownFrom(from)));
    }

    [Fact]
    public async Task Closing_the_door_on_a_company_with_a_wind_down_date_runs_the_sweep_again_and_a_frozen_company_refuses_it()
    {
        await ArrangeTwoCompaniesAsync();
        var from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        HttpAssert.IsOk(await AdminB().PostAsJsonAsync(WindDownRoute, WindDownFrom(from)));
        Assert.Single(await WindDownMessagesAsync());

        await WaitForTheNextSecondAsync();
        await DeactivateBAsync();

        var messages = await WindDownMessagesAsync();
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal(HostTestTenants.B, m.TenantId));
        Assert.Equal(2, messages.Select(m => m.MessageKey).Distinct().Count());

        await SeedAsync(async ctx =>
        {
            var tenant = await ctx.Tenants.SingleAsync(t => t.Id == HostTestTenants.B);
            tenant.RequestArchive(AdminBId, DateTimeOffset.UtcNow);
        });
        await HttpAssert.AssertBusinessErrorAsync(
            await AdminB().PostAsJsonAsync(WindDownRoute, WindDownFrom(null)), BusinessErrorMessage.CompanyArchived);
        Assert.Equal(2, (await WindDownMessagesAsync()).Count);
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
