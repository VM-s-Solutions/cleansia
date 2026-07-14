using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.HostTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0024 (TC-REVOKE-TTL-2) — access-token expiry is enforced with ClockSkew=Zero semantics
/// end-to-end on the mobile host, making <c>AccessTokenExpMinutes</c> the exact device-revocation
/// latency bound. The real mobile host is booted with the TTL overridden to a fractional value
/// (~3 s — the setting is a <c>double</c>), layered LAST so neither the host's own appsettings nor
/// the appsettings.HostTests.json overlay masks it. A token the host itself minted authenticates
/// while fresh and is rejected 401 once real time passes its expiry. Under the default 5-minute
/// clock skew a 3-second-expired token would still pass, so this fails if ClockSkew=Zero regresses.
/// No fake clock: both mint sites use raw DateTime.UtcNow, which this test must not touch.
/// </summary>
public sealed class MobileAccessTokenTtlExpiryTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Email = "ttl-expiry@hosttests.local";
    private const string Password = "12345678Test!";

    [Fact]
    public async Task Expired_mobile_access_token_is_rejected_401_with_zero_clock_skew()
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            ctx.Users.Add(DomainSeed.EmployeeUser(Email));
        });

        using var shortTtlFactory = MobileHost.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:AccessTokenExpMinutes"] = "0.05",
                })));
        using var client = shortTtlFactory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/Auth/Login", new
        {
            email = Email,
            password = Password,
            rememberMe = true,
        });
        HttpAssert.IsOk(login);

        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var accessToken = body.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));

        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        var freshCall = await client.GetAsync("/api/Device/Mine");
        HttpAssert.IsOk(freshCall);

        // 3 s TTL + up to 1 s lost to the exp claim's whole-second truncation + margin.
        await Task.Delay(TimeSpan.FromSeconds(5));

        var staleCall = await client.GetAsync("/api/Device/Mine");
        HttpAssert.IsUnauthorized(staleCall);
    }
}
