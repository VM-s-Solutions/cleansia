using System.Security.Claims;
using System.Text.Encodings.Web;
using Cleansia.Config.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cleansia.Tests.RateLimiting.Harness;

/// <summary>
/// ADR-0003 Rollout #1 — a reusable <see cref="TestServer"/> host harness that boots the REAL
/// rate-limiter middleware pipeline (the same <see cref="RateLimitPolicies"/> registration +
/// <see cref="ForwardedHeadersOptions"/> + guard the production <c>CleansiaStartupBase</c> uses)
/// WITHOUT requiring Postgres / JWT / Stripe / the full host. It composes the production pipeline
/// pieces in the ADR-mandated order (<c>UseForwardedHeaders → UseAuthentication → UseRateLimiter</c>)
/// over stub endpoints carrying the genuine <c>"auth"</c> / <c>"interactive"</c> named policies, and
/// lets a test drive requests with a synthetic connection peer IP, a synthetic X-Forwarded-For
/// client IP, and an authenticated subject (the <c>X-Test-Sub</c> header).
///
/// This is the shared boot fixture (limiter isolation cases) and future host-level authz
/// tests need; keep it generic.
/// </summary>
public sealed class RateLimiterHostHarness : IAsyncDisposable
{
    public const string TestSubHeader = "X-Test-Sub";

    /// <summary>The default trusted proxy peer used in tests (matches the harness config below).</summary>
    public const string TrustedProxyPeer = "127.0.0.1";

    private readonly IHost _host;
    public TestServer Server { get; }

    private RateLimiterHostHarness(IHost host)
    {
        _host = host;
        Server = host.GetTestServer();
    }

    public static async Task<RateLimiterHostHarness> StartAsync(
        IDictionary<string, string?> config,
        string environmentName = "Production",
        Action<IEndpointRouteBuilder>? extraEndpoints = null)
    {
        var host = BuildHost(config, environmentName, extraEndpoints);
        await host.StartAsync();
        return new RateLimiterHostHarness(host);
    }

    /// <summary>Builds + starts a host and returns the thrown exception (the boot-guard assertion),
    /// or null if it booted successfully.</summary>
    public static async Task<Exception?> TryBootThrows(
        IDictionary<string, string?> config,
        string environmentName = "Production")
    {
        try
        {
            await using var h = await StartAsync(config, environmentName);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>
    /// Send one request through the real pipeline. <paramref name="path"/> is "/auth",
    /// "/interactive", or "/api/Payment/webhook". <paramref name="connectionPeer"/> is the immediate
    /// TCP peer (the "proxy");
    /// default is the trusted peer. <paramref name="xffClientIp"/> is the synthetic X-Forwarded-For
    /// client IP. <paramref name="sub"/>, when set, authenticates the request as that subject.
    /// Returns the resulting status code and the client IP the pipeline resolved.
    /// </summary>
    public async Task<RlResponse> SendAsync(
        string path,
        string? xffClientIp = null,
        string? sub = null,
        string connectionPeer = TrustedProxyPeer)
    {
        var ctx = await Server.SendAsync(c =>
        {
            c.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(connectionPeer);
            c.Request.Method = "POST";
            c.Request.Path = path;
            if (xffClientIp is not null) c.Request.Headers["X-Forwarded-For"] = xffClientIp;
            if (sub is not null) c.Request.Headers[TestSubHeader] = sub;
        });

        var resolvedIp = ctx.Items.TryGetValue("resolved-ip", out var r) ? r as string : null;
        var retryAfter = ctx.Response.Headers.TryGetValue("Retry-After", out var ra) ? ra.ToString() : null;
        return new RlResponse(ctx.Response.StatusCode, resolvedIp ?? ctx.Connection.RemoteIpAddress?.ToString(), retryAfter);
    }

    public readonly record struct RlResponse(int StatusCode, string? ResolvedClientIp, string? RetryAfter)
    {
        public bool Is429 => StatusCode == StatusCodes.Status429TooManyRequests;
    }

    private static IHost BuildHost(
        IDictionary<string, string?> config,
        string environmentName,
        Action<IEndpointRouteBuilder>? extraEndpoints = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();

        return new HostBuilder()
            .UseEnvironment(environmentName)
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
                    services.AddRouting();

                    // Minimal test authentication: an X-Test-Sub header yields an authenticated
                    // principal with that NameIdentifier; absence ⇒ anonymous. Exercises the real
                    // UseAuthentication → UseRateLimiter order so the per-`sub` branch fires.
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                    services.AddAuthorization();

                    // The REAL production registrations (the same code path CleansiaStartupBase uses).
                    RateLimitPolicies.ConfigureForwardedHeaders(services, configuration, ctx.HostingEnvironment);
                    RateLimitPolicies.AddCleansiaRateLimiter(services, configuration);
                });
                web.Configure(app =>
                {
                    // ADR-0003 D4 ordering (the band this harness exercises):
                    app.UseForwardedHeaders();   // top — real client IP from trusted XFF
                    app.Use((c, next) => { c.Items["resolved-ip"] = c.Connection.RemoteIpAddress?.ToString(); return next(); });
                    app.UseRouting();
                    app.UseAuthentication();     // populates HttpContext.User (sub branch)
                    app.UseRateLimiter();        // AFTER authentication
                    app.UseAuthorization();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/auth", Ok).RequireRateLimiting("auth");
                        endpoints.MapPost("/interactive", Ok).RequireRateLimiting("interactive");
                        // SEC-W3 — the genuine "webhook" named policy on the real Stripe
                        // webhook route, so the per-IP isolation / independence / Retry-After cases
                        // exercise the production policy through the production pipeline. AllowAnonymous
                        // mirrors the controller (Stripe is unauthenticated); the limiter still applies.
                        endpoints.MapPost("/api/Payment/webhook", Ok)
                            .AllowAnonymous()
                            .RequireRateLimiting("webhook");

                        // Optional, test-supplied routes that mirror a SPECIFIC production endpoint
                        // shape (e.g. a T-0194 remediation-target anonymous password path) so a
                        // runtime flood proves that endpoint's window is live middleware, not just
                        // attributed. Additive: null preserves the three canonical stub routes above.
                        extraEndpoints?.Invoke(endpoints);
                    });
                });
            })
            .Build();

        static Task Ok(HttpContext c)
        {
            c.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>Authentication handler that reads X-Test-Sub and authenticates as that subject.</summary>
    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(TestSubHeader, out var sub) || string.IsNullOrEmpty(sub))
                return Task.FromResult(AuthenticateResult.NoResult()); // anonymous

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, sub.ToString()) }, SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
