using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// End to end on both partner hosts: <c>POST api/Auth/RefreshToken</c> admits the profiles the host's
/// sign-in admits. An administrator signs in on a partner host (<c>PartnerLogin</c> serves Employee or
/// Administrator), so the session it was handed must refresh; a Customer holding a partner-audience
/// token is refused with the refresh key and nothing rotates. The customer case plants the row directly
/// — no partner host can mint it — so the audience matches and only the profile gate stands.
/// </summary>
public sealed class PartnerHostRefreshTokenRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Password = "12345678Test!";
    private const string AdminEmail = "refresh-admin@hosttests.local";
    private const string CustomerEmail = "refresh-customer@hosttests.local";
    private const string PartnerRefreshCookie = "partner_refresh_token=";

    private HttpClient AnonymousClientFor(string hostAudience) =>
        hostAudience == MobileAudience ? MobileClientAnonymous() : PartnerClientAnonymous();

    // The web partner host answers a sign-in with HttpOnly cookies and strips the body tokens; the
    // mobile host returns them in the body.
    private static async Task<string> RefreshTokenFromAsync(HttpResponseMessage login, string hostAudience)
    {
        if (hostAudience == MobileAudience)
        {
            using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("refreshToken").GetString()!;
        }

        var cookie = login.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(PartnerRefreshCookie, StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie[PartnerRefreshCookie.Length..].Split(';')[0]);
    }

    private Task<List<RefreshTokenEntity>> TokensOfAsync(string userId) =>
        QueryAsync(ctx => ctx.RefreshTokens.IgnoreQueryFilters().Where(t => t.UserId == userId).ToListAsync());

    [Theory]
    [InlineData(PartnerAudience)]
    [InlineData(MobileAudience)]
    public async Task An_Administrators_Partner_Session_Refreshes(string hostAudience)
    {
        var admin = DomainSeed.Admin(AdminEmail);
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            ctx.Users.Add(admin);
        });

        var login = await AnonymousClientFor(hostAudience)
            .PostAsJsonAsync("/api/Auth/Login", new { email = AdminEmail, password = Password, rememberMe = true });
        HttpAssert.IsOk(login);
        var refreshToken = await RefreshTokenFromAsync(login, hostAudience);

        var refresh = await AnonymousClientFor(hostAudience)
            .PostAsJsonAsync("/api/Auth/RefreshToken", new { token = refreshToken });

        HttpAssert.IsOk(refresh);
        var session = await refresh.Content.ReadFromJsonAsync<SessionBody>();
        Assert.Equal("Administrator", session!.Role);

        var tokens = await TokensOfAsync(admin.Id);
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, t => t.RevokedReason == "rotated");
    }

    [Theory]
    [InlineData(PartnerAudience)]
    [InlineData(MobileAudience)]
    public async Task A_Customers_Partner_Audience_Token_Is_Refused_And_Not_Rotated(string hostAudience)
    {
        const string rawToken = "planted-customer-partner-refresh";
        var customer = DomainSeed.Customer(CustomerEmail);
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            ctx.Users.Add(customer);
            ctx.RefreshTokens.Add(RefreshTokenEntity.Create(
                userId: customer.Id,
                tokenHash: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant(),
                expiresAt: DateTimeOffset.UtcNow.AddDays(7),
                audience: hostAudience,
                deviceLabel: null,
                ipAddress: null));
        });

        var refresh = await AnonymousClientFor(hostAudience)
            .PostAsJsonAsync("/api/Auth/RefreshToken", new { token = rawToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Contains(BusinessErrorMessage.InvalidRefreshToken, await refresh.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var tokens = await TokensOfAsync(customer.Id);
        var planted = Assert.Single(tokens);
        Assert.Null(planted.RevokedReason);
    }

    private sealed record SessionBody(bool IsEmailConfirmed, string? Role);
}
