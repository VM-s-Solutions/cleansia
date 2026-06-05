using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Cleansia.Config.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// Proves the production-posture CSRF flip. The three cookie-auth hosts shipped
/// <c>"Csrf": { "Enabled": false }</c> in prod, so <see cref="CsrfValidationMiddleware"/> short-circuited
/// (CsrfValidationMiddleware.cs:48) and every state-changing cookie-auth endpoint was reachable
/// cross-site on the victim's ambient cookie. This test boots the REAL CSRF wiring
/// (<see cref="CsrfMiddlewareExtensions.AddCsrfProtection"/> + <see cref="CsrfValidationMiddleware"/>)
/// from a production-like config and asserts:
///   - with <c>Csrf:Enabled=true</c> (the post-fix prod posture): an authenticated state-changing POST
///     WITHOUT an X-CSRF-Token header is rejected 403 (the middleware enforces);
///   - the matching X-CSRF-Token is accepted (200), so the flip does not break legitimate clients;
///   - GET is never gated (CSRF is about state mutation);
///   - control: with <c>Enabled=false</c> (the OLD prod posture) the same request slips through 200 —
///     this is the regression the config flip closes.
/// The Secret stays the prod placeholder semantics — provisioned out-of-band; here a fixed test secret.
/// </summary>
public class CsrfProductionConfigTests
{
    private const string TestSecret = "test-csrf-secret-value";
    private const string SessionSub = "user-123";

    private static IConfiguration ProdLikeConfig(bool csrfEnabled) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Csrf:Enabled"] = csrfEnabled ? "true" : "false",
            ["Csrf:Secret"] = TestSecret,
        }).Build();

    private static IHost BuildHost(bool csrfEnabled)
    {
        var configuration = ProdLikeConfig(csrfEnabled);
        return new HostBuilder()
            .UseEnvironment(Environments.Production)
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(configuration);
                    services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
                    services.AddRouting();
                    services.AddAuthentication(CookieLikeAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, CookieLikeAuthHandler>(
                            CookieLikeAuthHandler.SchemeName, _ => { });
                    services.AddAuthorization();
                    // The REAL production CSRF registration — binds Csrf:Enabled / Csrf:Secret.
                    services.AddCsrfProtection(configuration, optOutPaths: Array.Empty<string>());
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseCsrfValidation(); // the real middleware, in its production position
                    app.UseAuthorization();
                    app.UseEndpoints(e =>
                    {
                        e.MapPost("/api/Order/Cancel", c => { c.Response.StatusCode = 200; return Task.CompletedTask; });
                        e.MapGet("/api/Order/Get", c => { c.Response.StatusCode = 200; return Task.CompletedTask; });
                    });
                });
            })
            .Build();
    }

    private static string ExpectedToken() => new CsrfTokenService(TestSecret).Derive(SessionSub);

    [Fact]
    public async Task ProdEnabled_StateChanging_Without_Token_Is_Rejected_403()
    {
        using var host = BuildHost(csrfEnabled: true);
        await host.StartAsync();
        var ctx = await host.GetTestServer().SendAsync(c =>
        {
            c.Request.Method = "POST";
            c.Request.Path = "/api/Order/Cancel";
            c.Request.Headers[CookieLikeAuthHandler.AuthHeader] = SessionSub;
            // No X-CSRF-Token header — the cross-site forged request shape.
        });
        Assert.Equal((int)HttpStatusCode.Forbidden, ctx.Response.StatusCode);
        await host.StopAsync();
    }

    [Fact]
    public async Task ProdEnabled_StateChanging_With_Valid_Token_Is_Accepted_200()
    {
        using var host = BuildHost(csrfEnabled: true);
        await host.StartAsync();
        var ctx = await host.GetTestServer().SendAsync(c =>
        {
            c.Request.Method = "POST";
            c.Request.Path = "/api/Order/Cancel";
            c.Request.Headers[CookieLikeAuthHandler.AuthHeader] = SessionSub;
            c.Request.Headers["X-CSRF-Token"] = ExpectedToken();
        });
        Assert.Equal((int)HttpStatusCode.OK, ctx.Response.StatusCode);
        await host.StopAsync();
    }

    [Fact]
    public async Task ProdEnabled_Get_Is_Never_Gated()
    {
        using var host = BuildHost(csrfEnabled: true);
        await host.StartAsync();
        var ctx = await host.GetTestServer().SendAsync(c =>
        {
            c.Request.Method = "GET";
            c.Request.Path = "/api/Order/Get";
            c.Request.Headers[CookieLikeAuthHandler.AuthHeader] = SessionSub;
        });
        Assert.Equal((int)HttpStatusCode.OK, ctx.Response.StatusCode);
        await host.StopAsync();
    }

    [Fact]
    public async Task Control_OldProdPosture_Disabled_Lets_Forged_Request_Through_200()
    {
        // The pre-fix prod config (Enabled=false). This is the hole the flip closes.
        using var host = BuildHost(csrfEnabled: false);
        await host.StartAsync();
        var ctx = await host.GetTestServer().SendAsync(c =>
        {
            c.Request.Method = "POST";
            c.Request.Path = "/api/Order/Cancel";
            c.Request.Headers[CookieLikeAuthHandler.AuthHeader] = SessionSub;
        });
        Assert.Equal((int)HttpStatusCode.OK, ctx.Response.StatusCode);
        await host.StopAsync();
    }

    /// <summary>Authenticates as a session subject when the auth header is present, mirroring the
    /// HttpOnly-cookie auth surface: it puts a <c>sub</c> claim on the principal so
    /// <see cref="CsrfTokenService.GetSessionKey"/> can derive the expected token.</summary>
    private sealed class CookieLikeAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "CookieLike";
        public const string AuthHeader = "X-Test-Session";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(AuthHeader, out var sub) || string.IsNullOrEmpty(sub))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, sub.ToString()) }, SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
