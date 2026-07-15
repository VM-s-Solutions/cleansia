using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Config.Services.DeviceRevocation;
using Cleansia.HostTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0026 (T-0414) end-to-end on the REAL mobile Partner host: a device deleted from the Devices
/// list loses API access within the poll bound. Its outstanding, still-unexpired access token
/// authenticates while fresh and 401s once the RevokedDeviceDirectory has seen the revocation — the
/// full OnTokenValidated enforcement pipeline, not a stubbed principal. Enforcement is device-keyed,
/// re-login self-heals via the iat guard, claim-less tokens pass, logout entries kill stolen tokens,
/// and a hostile re-registration cannot expunge the entry (the A1 resurrection pin).
///
/// The directory refresh is forced deterministically via the public
/// <see cref="RevokedDeviceDirectoryRefresher.RefreshOnceAsync"/> resolved from the booted host,
/// instead of racing the timer.
/// </summary>
public sealed class DeviceRevocationEnforcementTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Password = "12345678Test!";

    private async Task ForceDirectoryRefreshAsync()
    {
        var refresher = MobileHost.Services.GetRequiredService<RevokedDeviceDirectoryRefresher>();
        await refresher.RefreshOnceAsync(CancellationToken.None);
    }

    private async Task SeedEmployeeAsync(string email)
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            ctx.Users.Add(DomainSeed.EmployeeUser(email));
        });
    }

    private async Task<(string Token, string RefreshToken)> LoginAsync(string email, string? deviceId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/Login")
        {
            Content = JsonContent.Create(new { email, password = Password, rememberMe = true }),
        };
        if (deviceId is not null)
        {
            request.Headers.Add("X-Device-Id", deviceId);
        }

        using var client = MobileHost.CreateClient();
        var response = await client.SendAsync(request);
        HttpAssert.IsOk(response);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("token").GetString()!;
        var refresh = body.RootElement.GetProperty("refreshToken").GetString()!;
        return (token, refresh);
    }

    private async Task<string> RegisterDeviceAsync(string token, string deviceId)
    {
        using var client = MobileClient(token);
        var response = await client.PostAsJsonAsync("/api/Device/Register", new
        {
            deviceId,
            deviceToken = $"push-{deviceId}",
            platform = "android",
        });
        HttpAssert.IsOk(response);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("deviceId").GetString()!;
    }

    private async Task RevokeDeviceAsync(string token, string deviceRowId)
    {
        using var client = MobileClient(token);
        var response = await client.DeleteAsync($"/api/Device/{deviceRowId}");
        HttpAssert.IsOk(response);
    }

    private async Task<HttpStatusCode> CallAuthedAsync(string token)
    {
        using var client = MobileClient(token);
        var response = await client.GetAsync("/api/Device/Mine");
        return response.StatusCode;
    }

    // TC-REVOKE-NOW-1 — the headline property.
    [Fact]
    public async Task Revoked_device_existing_access_token_gets_401_after_refresh_and_other_user_unaffected()
    {
        await SeedEmployeeAsync("revoke-now-1@hosttests.local");
        await SeedEmployeeAsync("revoke-now-1-other@hosttests.local");

        var (token, _) = await LoginAsync("revoke-now-1@hosttests.local", deviceId: "A");
        var (otherToken, _) = await LoginAsync("revoke-now-1-other@hosttests.local", deviceId: "OTHER");
        var rowId = await RegisterDeviceAsync(token, "A");

        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(token));

        await RevokeDeviceAsync(token, rowId);
        await ForceDirectoryRefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(token));
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(otherToken));
    }

    // TC-REVOKE-NOW-2 — sibling-session precision (device-keyed, never user-keyed).
    [Fact]
    public async Task Sibling_device_of_same_user_stays_200_after_revoking_the_other()
    {
        await SeedEmployeeAsync("revoke-now-2@hosttests.local");

        var (tokenA, _) = await LoginAsync("revoke-now-2@hosttests.local", deviceId: "A");
        var (tokenB, _) = await LoginAsync("revoke-now-2@hosttests.local", deviceId: "B");
        var rowA = await RegisterDeviceAsync(tokenA, "A");
        await RegisterDeviceAsync(tokenB, "B");

        await RevokeDeviceAsync(tokenA, rowA);
        await ForceDirectoryRefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(tokenA));
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(tokenB));
    }

    // TC-REVOKE-NOW-3 — the iat guard / re-login self-heal (revoke kills sessions, not devices).
    [Fact]
    public async Task Relogin_on_revoked_device_gets_a_token_that_passes_while_entry_present()
    {
        await SeedEmployeeAsync("revoke-now-3@hosttests.local");

        var (token, _) = await LoginAsync("revoke-now-3@hosttests.local", deviceId: "A");
        var rowId = await RegisterDeviceAsync(token, "A");

        await RevokeDeviceAsync(token, rowId);
        await ForceDirectoryRefreshAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(token));

        // iat is a whole-second NumericDate while DeactivatedOn is sub-second; a re-login inside the same
        // wall-clock second as the revoke carries iat < RevokedAt and would 401 once then self-heal via
        // refresh (ADR-0026 D9.3). Cross the second boundary so the re-login deterministically postdates it.
        await WaitPastNextSecondAsync();

        // Fresh login on the same device — iat now postdates the revocation, the entry is still present.
        var (freshToken, _) = await LoginAsync("revoke-now-3@hosttests.local", deviceId: "A");
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(freshToken));
    }

    // TC-REVOKE-NOW-5 — transition fail-open: a claim-less token for a user with an entry still passes.
    [Fact]
    public async Task Claim_less_token_passes_even_when_the_user_has_a_directory_entry()
    {
        await SeedEmployeeAsync("revoke-now-5@hosttests.local");

        // Device-claimed session to create + revoke a device row (seeds a directory entry for this user).
        var (claimedToken, _) = await LoginAsync("revoke-now-5@hosttests.local", deviceId: "A");
        var rowId = await RegisterDeviceAsync(claimedToken, "A");
        await RevokeDeviceAsync(claimedToken, rowId);

        // A login WITHOUT X-Device-Id mints a token with no device_id claim — it can never match.
        var (claimlessToken, _) = await LoginAsync("revoke-now-5@hosttests.local", deviceId: null);
        await ForceDirectoryRefreshAsync();

        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(claimlessToken));
    }

    // TC-REVOKE-NOW-8 — logout (UnregisterDevice) entries kill a stolen still-valid access token.
    [Fact]
    public async Task Logout_unregister_kills_the_outstanding_access_token_after_refresh()
    {
        await SeedEmployeeAsync("revoke-now-8@hosttests.local");

        var (token, _) = await LoginAsync("revoke-now-8@hosttests.local", deviceId: "A");
        await RegisterDeviceAsync(token, "A");
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(token));

        using (var client = MobileClient(token))
        {
            var unregister = await client.DeleteAsync("/api/Device/Unregister?deviceId=A");
            HttpAssert.IsOk(unregister);
        }

        await ForceDirectoryRefreshAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(token));
    }

    // TC-REVOKE-NOW-9 (A1) — hostile re-registration cannot expunge the enforcement entry.
    [Fact]
    public async Task Re_registration_after_revoke_does_not_rescue_the_old_token()
    {
        await SeedEmployeeAsync("revoke-now-9@hosttests.local");

        var (token, _) = await LoginAsync("revoke-now-9@hosttests.local", deviceId: "A");
        var rowId = await RegisterDeviceAsync(token, "A");

        await RevokeDeviceAsync(token, rowId);

        // Before any directory refresh: the still-valid access token re-registers device A. MarkRegistered
        // reactivates the row (IsActive=true) but never clears DeactivatedOn.
        await RegisterDeviceAsync(token, "A");

        await ForceDirectoryRefreshAsync();

        // The snapshot keys on DeactivatedOn, not row state — the old token still 401s.
        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(token));

        // A fresh login on A afterwards passes (the iat guard, unchanged); cross the second boundary so
        // the fresh iat deterministically postdates the sub-second revocation stamp (ADR-0026 D9.3).
        await WaitPastNextSecondAsync();
        var (freshToken, _) = await LoginAsync("revoke-now-9@hosttests.local", deviceId: "A");
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(freshToken));
    }

    // AC2 chain — the revoked device's REFRESH is already dead (InvalidRefreshToken), so the client's
    // 401 -> refresh -> refresh-rejected path forces sign-out. This pins the refresh-dead leg.
    [Fact]
    public async Task Revoked_device_refresh_token_is_rejected_so_the_client_forced_signout_fires()
    {
        await SeedEmployeeAsync("revoke-now-chain@hosttests.local");

        var (token, refreshToken) = await LoginAsync("revoke-now-chain@hosttests.local", deviceId: "A");
        var rowId = await RegisterDeviceAsync(token, "A");

        await RevokeDeviceAsync(token, rowId);
        await ForceDirectoryRefreshAsync();

        // Leg 1: the access token 401s.
        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(token));

        // Leg 2: the client single-flights a refresh — which is already revoked (device_revoked) and
        // rejected. The refresh returns 401 with InvalidRefreshToken — a REJECTED refresh (not merely
        // unavailable) is what wipes the session and forces sign-out client-side.
        using var client = MobileHost.CreateClient();
        var refresh = await client.PostAsJsonAsync("/api/Auth/RefreshToken", new { token = refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        using var body = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        Assert.Contains("auth.invalid_refresh_token", body.RootElement.ToString());
    }

    // iat NumericDate is whole-second; DeactivatedOn is sub-second. Wait until the wall clock crosses
    // into the next whole second so a subsequent login's truncated iat strictly postdates the revocation.
    private static async Task WaitPastNextSecondAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var msIntoSecond = now.Millisecond;
        await Task.Delay(TimeSpan.FromMilliseconds(1000 - msIntoSecond + 100));
    }
}
