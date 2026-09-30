using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// A web customer whose access token has lapsed still sends it. On the routes that serve guests and
/// customers alike, serving that request as a guest books without the account, its consent or its
/// member price; answering 401 makes the client refresh and replay it as the customer. The refresh
/// itself arrives carrying the lapsed token, so it must still be served, and a refused refresh drops
/// both cookies so the browser is a plain guest afterwards rather than 401'd on every guest route.
/// </summary>
public sealed class CustomerWebExpiredSessionTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string CustomerId = "expired-session-customer";
    private const string CustomerEmail = "expired-session-customer@hosttests.local";
    private const string AccessCookie = "customer_token";
    private const string RefreshCookie = "customer_refresh_token";

    public static TheoryData<string> RoutesServingGuestsAndCustomers =>
    [
        "/api/Order/Quote",
        "/api/Order/QuotePlusSavings",
        "/api/Order/CreateOrder",
        "/api/Payment/CreateOrder",
    ];

    private HttpClient ClientCarrying(string cookies)
    {
        var client = CustomerHost.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add(HeaderNames.Cookie, cookies);
        return client;
    }

    private static string CustomerToken(bool expired) =>
        TestJwtFactory.Mint(CustomerAudience, CustomerId, CustomerEmail, UserProfile.Customer, expired: expired);

    private Task SeedCustomerAsync() => SeedAsync(async ctx =>
    {
        await DomainSeed.EnsureReferenceDataAsync(ctx);
        var customer = DomainSeed.Customer(CustomerEmail);
        customer.Id = CustomerId;
        ctx.Users.Add(customer);
    });

    [Theory]
    [MemberData(nameof(RoutesServingGuestsAndCustomers))]
    public async Task A_lapsed_session_is_answered_401_instead_of_being_served_as_a_guest(string route)
    {
        using var client = ClientCarrying($"{AccessCookie}={CustomerToken(expired: true)}");

        HttpAssert.IsUnauthorized(await client.PostAsJsonAsync(route, new { }));
    }

    [Theory]
    [MemberData(nameof(RoutesServingGuestsAndCustomers))]
    public async Task A_guest_and_a_live_session_are_both_served(string route)
    {
        await SeedCustomerAsync();
        using var live = ClientCarrying($"{AccessCookie}={CustomerToken(expired: false)}");

        HttpAssert.ClearedTheGate(await CustomerClientAnonymous().PostAsJsonAsync(route, new { }));
        HttpAssert.ClearedTheGate(await live.PostAsJsonAsync(route, new { }));
    }

    // A read writes nothing as a guest, and the booking map is a browser <img> that never passes
    // through the client's refresh interceptor: a 401 would only break the picture.
    [Fact]
    public async Task A_lapsed_session_still_reads_an_anonymous_route()
    {
        using var client = ClientCarrying($"{AccessCookie}={CustomerToken(expired: true)}");

        // 404 is the unprovisioned map provider; only a 401 would mean the session was refused.
        Assert.NotEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/AddressSearch/map?lat=50.08&lng=14.42")).StatusCode);
    }

    [Fact]
    public async Task The_refresh_is_served_although_it_carries_the_lapsed_access_token()
    {
        await SeedCustomerAsync();
        var login = await CustomerClientAnonymous().PostAsJsonAsync(
            "/api/Auth/Login", new { email = CustomerEmail, password = "12345678Test!", rememberMe = true });
        HttpAssert.IsOk(login);
        var refreshToken = SetCookies(login).Single(c => c.Name == RefreshCookie).Value.Value;
        using var client = ClientCarrying($"{AccessCookie}={CustomerToken(expired: true)}; {RefreshCookie}={refreshToken}");

        HttpAssert.IsOk(await client.PostAsJsonAsync("/api/Auth/RefreshToken", new { token = "" }));
    }

    [Fact]
    public async Task A_refused_refresh_drops_both_session_cookies()
    {
        using var client = ClientCarrying($"{AccessCookie}={CustomerToken(expired: true)}; {RefreshCookie}=revoked-or-unknown");

        var refresh = await client.PostAsJsonAsync("/api/Auth/RefreshToken", new { token = "" });

        HttpAssert.IsUnauthorized(refresh);
        var cleared = SetCookies(refresh)
            .Where(c => string.IsNullOrEmpty(c.Value.Value) && c.Expires < DateTimeOffset.UtcNow)
            .Select(c => c.Name.Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(AccessCookie, cleared);
        Assert.Contains(RefreshCookie, cleared);
    }

    private static IList<SetCookieHeaderValue> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? SetCookieHeaderValue.ParseList(values.ToList())
            : [];
}
