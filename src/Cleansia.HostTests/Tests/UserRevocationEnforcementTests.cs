using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Config.Services.UserRevocation;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.HostTests.Infrastructure;
using Cleansia.Infra.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// ADR-0027 (T-0418) end-to-end on the REAL mobile Partner host: a password RESET ends that user's
/// mobile access within the poll bound. The outstanding, still-unexpired PRE-reset access token
/// authenticates while fresh and 401s once the RevokedUserDirectory has seen the reset — the full
/// OnTokenValidated enforcement pipeline (device probe then user probe), not a stubbed principal.
/// Enforcement is sub-keyed (scoped to the reset user), the post-reset re-login self-heals via the iat
/// guard including the same-wall-clock-second boundary, claim-less tokens are cut off too, and password
/// CHANGE is deliberately NOT accelerated.
///
/// The reset revocation is driven the way ChangePassword.Handler drives it — through the booted host's
/// own IRefreshTokenService.RevokeAllForUserAsync (keep-none, reason "password_reset") committed on the
/// host's CleansiaDbContext — so the poll reads exactly the rows a real reset writes. The directory
/// refresh is forced deterministically via the public RefreshOnceAsync resolved from the booted host.
/// </summary>
public sealed class UserRevocationEnforcementTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string Password = "12345678Test!";

    private async Task ForceDirectoryRefreshAsync()
    {
        var refresher = MobileHost.Services.GetRequiredService<RevokedUserDirectoryRefresher>();
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

    private async Task<(string Token, string RefreshToken, string UserId)> LoginAsync(string email, string? deviceId = "A")
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
        var userId = body.RootElement.GetProperty("userId").GetString()!;
        return (token, refresh, userId);
    }

    // Drive the SAME domain path ChangePassword.Handler (reset) / ChangeOwnPassword.Handler (change)
    // drive: RevokeAllForUserAsync through the host's own service + DbContext commit.
    private async Task RevokeAllForUserAsync(string userId, string reason, string? exceptRawToken)
    {
        using var scope = MobileHost.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var ctx = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        await service.RevokeAllForUserAsync(userId, reason, exceptRawToken, CancellationToken.None);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<HttpStatusCode> CallAuthedAsync(string token)
    {
        using var client = MobileClient(token);
        var response = await client.GetAsync("/api/Device/Mine");
        return response.StatusCode;
    }

    // TC-REVOKE-USER-1 — the headline property.
    [Fact]
    public async Task Reset_pre_reset_access_token_gets_401_after_refresh_and_other_user_unaffected()
    {
        await SeedEmployeeAsync("revoke-user-1@hosttests.local");
        await SeedEmployeeAsync("revoke-user-1-other@hosttests.local");

        var (token, _, userId) = await LoginAsync("revoke-user-1@hosttests.local");
        var (otherToken, _, _) = await LoginAsync("revoke-user-1-other@hosttests.local");

        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(token));

        await RevokeAllForUserAsync(userId, "password_reset", exceptRawToken: null);
        await ForceDirectoryRefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(token));
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(otherToken));
    }

    // TC-REVOKE-USER-1 (AC2 chain) — the reset user's REFRESH is already dead (password_reset →
    // InvalidRefreshToken), so the client's 401 → refresh → refresh-rejected path forces sign-out.
    [Fact]
    public async Task Reset_refresh_token_is_rejected_so_the_client_forced_signout_fires()
    {
        await SeedEmployeeAsync("revoke-user-chain@hosttests.local");

        var (token, refreshToken, userId) = await LoginAsync("revoke-user-chain@hosttests.local");

        await RevokeAllForUserAsync(userId, "password_reset", exceptRawToken: null);
        await ForceDirectoryRefreshAsync();

        // Leg 1: the pre-reset access token 401s.
        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(token));

        // Leg 2: the client single-flights a refresh — already revoked (password_reset) and rejected. A
        // REJECTED refresh (not merely unavailable) is what wipes the session and forces sign-out.
        using var client = MobileHost.CreateClient();
        var refresh = await client.PostAsJsonAsync("/api/Auth/RefreshToken", new { token = refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        using var body = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        Assert.Contains("auth.invalid_refresh_token", body.RootElement.ToString());
    }

    // TC-REVOKE-USER-2 — post-reset re-login passes (the iat guard: reset kills sessions, not the user).
    [Fact]
    public async Task Post_reset_relogin_passes_while_the_entry_is_still_present()
    {
        await SeedEmployeeAsync("revoke-user-2@hosttests.local");

        var (token, _, userId) = await LoginAsync("revoke-user-2@hosttests.local");

        await RevokeAllForUserAsync(userId, "password_reset", exceptRawToken: null);
        await ForceDirectoryRefreshAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(token));

        // iat is a whole-second NumericDate while RevokedAt is sub-second; cross the second boundary so
        // the re-login's iat deterministically postdates the reset (the boundary case is asserted below).
        await WaitPastNextSecondAsync();

        var (freshToken, _, _) = await LoginAsync("revoke-user-2@hosttests.local");
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(freshToken));
    }

    // TC-REVOKE-USER-2 boundary (panel U1) — a post-reset re-login whose iat truncates into the reset's
    // SAME wall-clock second (iat < RevokedAt sub-second) 401s ONCE and then self-heals: its refresh
    // token was minted AFTER the reset (never in the keep-none revoke set), so refresh succeeds and the
    // retried request 200s. "No lockout on the recovery path" as a proven property, not an assertion.
    [Fact]
    public async Task Same_wall_clock_second_relogin_401s_once_then_self_heals_via_its_live_refresh()
    {
        await SeedEmployeeAsync("revoke-user-2b@hosttests.local");

        var (initialToken, _, userId) = await LoginAsync("revoke-user-2b@hosttests.local");

        // Align to just after a whole-second tick, then reset + immediately re-login inside the same
        // wall-clock second so the fresh token's truncated iat can equal the reset second (iat < resetAt).
        await AlignToJustAfterSecondBoundaryAsync();
        await RevokeAllForUserAsync(userId, "password_reset", exceptRawToken: null);
        var (freshToken, freshRefresh, _) = await LoginAsync("revoke-user-2b@hosttests.local");

        await ForceDirectoryRefreshAsync();

        // The original pre-reset token is unambiguously killed.
        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(initialToken));

        // The fresh re-login may 401 once IF its truncated iat landed in the reset's same second. Whether
        // or not it did, its refresh token postdates the reset and is alive, so the self-heal is available.
        var firstCall = await CallAuthedAsync(freshToken);
        Assert.True(firstCall is HttpStatusCode.OK or HttpStatusCode.Unauthorized);

        // The load-bearing self-heal proof: the fresh session's refresh token was minted AFTER the reset,
        // so it was never in the keep-none revoke set — the refresh SUCCEEDS (a keep-none-revoked token
        // would 401 InvalidRefreshToken here and force sign-out). Cross the wall-clock second first so the
        // rotated access token's whole-second iat deterministically postdates the sub-second reset stamp.
        await WaitPastNextSecondAsync();
        using var client = MobileHost.CreateClient();
        var refresh = await client.PostAsJsonAsync("/api/Auth/RefreshToken", new { token = freshRefresh });
        HttpAssert.IsOk(refresh);
        using var body = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        var refreshedAccess = body.RootElement.GetProperty("token").GetString()!;

        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(refreshedAccess));
    }

    // TC-REVOKE-USER-3 — two users; reset A → A's pre-reset token 401s, B's stays 200 (sub-scoped).
    [Fact]
    public async Task Resetting_one_user_leaves_an_unrelated_users_token_untouched()
    {
        await SeedEmployeeAsync("revoke-user-3-a@hosttests.local");
        await SeedEmployeeAsync("revoke-user-3-b@hosttests.local");

        var (tokenA, _, userIdA) = await LoginAsync("revoke-user-3-a@hosttests.local");
        var (tokenB, _, _) = await LoginAsync("revoke-user-3-b@hosttests.local");

        await RevokeAllForUserAsync(userIdA, "password_reset", exceptRawToken: null);
        await ForceDirectoryRefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(tokenA));
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(tokenB));
    }

    // TC-REVOKE-USER-6 (the D3 pin) — password CHANGE ("password_changed") does NOT enter the user
    // directory, so an OTHER pre-change access token is NOT 401'd by the reset-cutoff check.
    [Fact]
    public async Task Password_change_is_not_accelerated_by_the_user_directory()
    {
        await SeedEmployeeAsync("revoke-user-6@hosttests.local");

        // Two sessions for the same user; "change" spares the caller's own and revokes the other with
        // reason "password_changed" — which the poll predicate (password_reset only) must ignore.
        var (callerToken, callerRefresh, userId) = await LoginAsync("revoke-user-6@hosttests.local", deviceId: "CALLER");
        var (otherToken, _, _) = await LoginAsync("revoke-user-6@hosttests.local", deviceId: "OTHER");

        await RevokeAllForUserAsync(userId, "password_changed", exceptRawToken: callerRefresh);
        await ForceDirectoryRefreshAsync();

        // Neither token is 401'd by the USER directory: password_changed is not fed in (the caller's own
        // token is spared entirely; the other token's refresh chain dies by TTL, but its still-valid
        // ACCESS token is not accelerated by this check).
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(callerToken));
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(otherToken));
    }

    // TC-REVOKE-USER-8 (D6 bonus) — a login WITHOUT X-Device-Id (no device_id claim) whose user is then
    // reset → its pre-reset access token 401s, keyed on sub which it carries.
    [Fact]
    public async Task Claim_less_mobile_session_is_cut_off_on_reset()
    {
        await SeedEmployeeAsync("revoke-user-8@hosttests.local");

        var (claimlessToken, _, userId) = await LoginAsync("revoke-user-8@hosttests.local", deviceId: null);
        Assert.Equal(HttpStatusCode.OK, await CallAuthedAsync(claimlessToken));

        await RevokeAllForUserAsync(userId, "password_reset", exceptRawToken: null);
        await ForceDirectoryRefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, await CallAuthedAsync(claimlessToken));
    }

    // iat NumericDate is whole-second; RevokedAt is sub-second. Wait until the wall clock crosses into
    // the next whole second so a subsequent login's truncated iat strictly postdates the revocation.
    private static async Task WaitPastNextSecondAsync()
    {
        var now = DateTimeOffset.UtcNow;
        await Task.Delay(TimeSpan.FromMilliseconds(1000 - now.Millisecond + 100));
    }

    // Wait until we are just past a whole-second boundary, so a reset + re-login that follow immediately
    // fall inside the same wall-clock second (the fresh token's truncated iat can equal the reset second).
    private static async Task AlignToJustAfterSecondBoundaryAsync()
    {
        var now = DateTimeOffset.UtcNow;
        await Task.Delay(TimeSpan.FromMilliseconds(1000 - now.Millisecond + 20));
    }
}
